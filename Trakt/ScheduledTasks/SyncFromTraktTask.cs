using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Trakt.Api;
using Trakt.Api.DataContracts;
using Trakt.Helpers;

namespace Trakt.ScheduledTasks
{

    /// <summary>
    /// Task that will Sync each users trakt.tv profile with their local library. This task will only include 
    /// watched states.
    /// </summary>
    [Export(typeof(IScheduledTask))]
    class SyncFromTraktTask : IScheduledTask
    {
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IUserManager _userManager;
        private readonly IUserDataRepository _userDataRepository;
        private readonly ILogger _logger;
        private readonly TraktApi _traktApi;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="logger"></param>
        /// <param name="httpClient"></param>
        /// <param name="jsonSerializer"></param>
        /// <param name="userManager"></param>
        /// <param name="userDataRepository"> </param>
        public SyncFromTraktTask(Kernel kernel, ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer, IUserManager userManager, IUserDataRepository userDataRepository)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _userManager = userManager;
            _userDataRepository = userDataRepository;
            _logger = logger;
            _traktApi = new TraktApi(_httpClient, _jsonSerializer, _logger);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var users = _userManager.Users.Where(u =>
            {
                var traktUser = UserHelper.GetTraktUser(u);

                return traktUser != null && traktUser.TraktLocations != null && traktUser.TraktLocations.Length > 0;

            }).ToList();

            // No point going further if we don't have users.
            if (users.Count == 0)
            {
                _logger.Info("TRAKT: No Users returned");
                return;
            }

            // purely for progress reporting
            var progPercent = 0.0;
            var percentPerUser = 100 / users.Count;

            foreach (var user in users)
            {
                var libraryRoot = user.RootFolder;
                var traktUser = UserHelper.GetTraktUser(user);

                /*
                 * In order to be as accurate as possible. We need to download the users show collection & the users watched shows.
                 * It's unfortunate that trakt.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
                 * like they do for movies.
                 */

                IEnumerable<TraktMovieDataContract> tMovies = await _traktApi.SendGetAllMoviesRequest(traktUser).ConfigureAwait(false);
                IEnumerable<TraktUserLibraryShowDataContract> tShowsCollection = await _traktApi.SendGetCollectionShowsRequest(traktUser).ConfigureAwait(false);
                IEnumerable<TraktUserLibraryShowDataContract> tShowsWatched = await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false);

                var mediaItems = libraryRoot.GetRecursiveChildren(user)
                    .Where(i => i is Episode || i is Movie)
                    .OrderBy(i =>
                    {
                        var episode = i as Episode;

                        return episode != null ? episode.SeriesItemId : i.Id;
                    })
                    .ToList();

                if (mediaItems.Count == 0) continue;

                // purely for progress reporting
                var percentPerItem = (double) percentPerUser / (double) mediaItems.Count;

                foreach (var child in mediaItems)
                {
                    if (child is Movie)
                    {
                        /* 
                         * First make sure this child is in the users collection. If not, skip it. if it is in the collection then we need
                         * to see if it's in the watched movies list. If it is, mark it watched, otherwise mark it unwatched.
                         */
                        var imdbId = child.GetProviderId(MetadataProviders.Imdb);
                        
                        if (imdbId == null) continue;

                        var matchedMovie = tMovies.SingleOrDefault(i => i.ImdbId == imdbId);

                        if (matchedMovie == null) continue;
                        
                        var userData = await _userDataRepository.GetUserData(user.Id, child.GetUserDataKey()).ConfigureAwait(false);

                        if (matchedMovie.Plays >= 1)
                        {
                            // set movie as watched
                            userData.Played = true;
                            userData.PlayCount = Math.Max(matchedMovie.Plays, userData.PlayCount); // keep the highest play count

                            // Set last played to whichever is most recent, remote or local time...
                            if (matchedMovie.LastPlayed > 0)
                            {
                                DateTime tLastPlayed = matchedMovie.LastPlayed.ConvertEpochToDateTime();
                                userData.LastPlayedDate = tLastPlayed > userData.LastPlayedDate
                                                                      ? tLastPlayed
                                                                      : userData.LastPlayedDate;
                            }
                        }
                        else
                        {
                            // set as unwatched
                            userData.Played = false;
                            userData.PlayCount = 0;
                        }

                        await _userDataRepository.SaveUserData(user.Id, child.GetUserDataKey(), userData,
                                                                     new CancellationToken());
                    }
                    else // Can only be an episode
                    {
                        /* 
                         * First make sure this child is in the users collection. If not, skip it. if it is in the collection then we need
                         * to see if it's in the watched shows list. If it is, mark it watched, otherwise mark it unwatched.
                         */

                        var tvdbId = child.GetProviderId(MetadataProviders.Tvdb);

                        if (tvdbId == null) continue;

                        var matchedShow = tShowsCollection.SingleOrDefault(tShow => tShow.TvdbId == tvdbId);

                        if (matchedShow == null) continue;

                        var matchedSeason = matchedShow.Seasons.SingleOrDefault(tSeason =>
                                                                        tSeason.Season == child.ParentIndexNumber);

                        if (matchedSeason == null || child.IndexNumber == null) continue;

                        var matchedEpisode = matchedSeason.Episodes.Contains((int)child.IndexNumber);

                        // if it's not a match then it means trakt doesn't know about the episode, leave the watched state alone and move on
                        if (!matchedEpisode) continue;

                        // episode is in users libary. Now we need to determine if it's watched

                        var userData =
                                await
                                _userDataRepository.GetUserData(user.Id, child.GetUserDataKey()).ConfigureAwait(false);

                        var watchedShowMatch = tShowsWatched.SingleOrDefault(tShow => tShow.TvdbId == tvdbId);

                        if (watchedShowMatch != null)
                        {
                            var watchedSeasonMatch =
                                watchedShowMatch.Seasons.SingleOrDefault(
                                    tSeason => tSeason.Season == child.ParentIndexNumber);

                            if (watchedSeasonMatch != null)
                            {
                                var watchedEpisode = watchedSeasonMatch.Episodes.Contains((int) child.IndexNumber);

                                if (watchedEpisode)
                                {
                                    userData.Played = true;
                                    await _userDataRepository.SaveUserData(user.Id, child.GetUserDataKey(), userData,
                                                                     new CancellationToken());
                                    continue;
                                }
                            }
                        }

                        userData.Played = false;
                        userData.PlayCount = 0;

                        await _userDataRepository.SaveUserData(user.Id, child.GetUserDataKey(), userData,
                                                                     new CancellationToken());
                    }

                    // purely for progress reporting
                    progPercent += percentPerItem;
                    progress.Report(progPercent);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {
            return new List<ITaskTrigger>();
        }

        /// <summary>
        /// 
        /// </summary>
        public string Name 
        {
            get { return "Import playstates from Trakt.tv"; }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Description
        {
            get { return "Sync's Watched/Unwatched status from Trakt.tv for each MB3 user that has a configured Trakt account"; }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Category
        {
            get { return "Trakt"; }
        }
    }
}
