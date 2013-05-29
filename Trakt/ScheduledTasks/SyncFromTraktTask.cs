using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
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
        /// <param name="jsonSerializer"></param>
        /// <param name="userManager"></param>
        /// <param name="userDataRepository"> </param>
        public SyncFromTraktTask(Kernel kernel, ILogger logger, IJsonSerializer jsonSerializer, IUserManager userManager, IUserDataRepository userDataRepository)
        {
            _jsonSerializer = jsonSerializer;
            _userManager = userManager;
            _userDataRepository = userDataRepository;
            _logger = logger;
            _traktApi = new TraktApi(_jsonSerializer, _logger);
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
            var percentPerUser = 100 / users.Count;
            double currentProgress = 0;
            var numComplete = 0;

            foreach (var user in users)
            {
                try
                {
                    await SyncTraktDataForUser(user, currentProgress, cancellationToken, progress, percentPerUser).ConfigureAwait(false);

                    numComplete++;
                    currentProgress = percentPerUser * numComplete;
                    progress.Report(currentProgress);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error syncing trakt data for user {0}", ex, user.Name);
                }
            }
        }

        private async Task SyncTraktDataForUser(User user, double currentProgress, CancellationToken cancellationToken, IProgress<double> progress, double percentPerUser)
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
                .Where(i =>
                {
                    var movie = i as Movie;

                    if (movie != null)
                    {
                        var imdbId = movie.GetProviderId(MetadataProviders.Imdb);

                        if (string.IsNullOrEmpty(imdbId))
                        {
                            return false;
                        }

                        return true;
                    }

                    var episode = i as Episode;

                    if (episode != null)
                    {
                        var tvdbId = episode.GetProviderId(MetadataProviders.Tvdb);

                        if (string.IsNullOrEmpty(tvdbId))
                        {
                            return false;
                        }

                        return true;
                    }

                    return false;
                })
                .OrderBy(i =>
                {
                    var episode = i as Episode;

                    return episode != null ? episode.SeriesItemId : i.Id;
                })
                .ToList();

            // purely for progress reporting
            var percentPerItem = (double)percentPerUser / (double)mediaItems.Count;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                /* 
                 * First make sure this child is in the users collection. If not, skip it. if it is in the collection then we need
                 * to see if it's in the watched movies list. If it is, mark it watched, otherwise mark it unwatched.
                 */
                var imdbId = movie.GetProviderId(MetadataProviders.Imdb);

                var matchedMovie = tMovies.FirstOrDefault(i => i.ImdbId == imdbId);

                if (matchedMovie != null)
                {
                    var userData = await _userDataRepository.GetUserData(user.Id, movie.GetUserDataKey()).ConfigureAwait(false);

                    if (matchedMovie.Plays >= 1)
                    {
                        // set movie as watched
                        userData.Played = true;
                        userData.PlayCount = Math.Max(matchedMovie.Plays, userData.PlayCount); // keep the highest play count

                        // Set last played to whichever is most recent, remote or local time...
                        if (matchedMovie.LastPlayed > 0)
                        {
                            var tLastPlayed = matchedMovie.LastPlayed.ConvertEpochToDateTime();
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
                        userData.LastPlayedDate = null;
                    }

                    await _userDataRepository.SaveUserData(user.Id, movie.GetUserDataKey(), userData, cancellationToken);
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            foreach (var episode in mediaItems.OfType<Episode>())
            {
                /* 
               * First make sure this child is in the users collection. If not, skip it. if it is in the collection then we need
               * to see if it's in the watched shows list. If it is, mark it watched, otherwise mark it unwatched.
               */

                var tvdbId = episode.GetProviderId(MetadataProviders.Tvdb);

                var matchedShow = tShowsCollection.FirstOrDefault(tShow => tShow.TvdbId == tvdbId);

                if (matchedShow != null)
                {
                    var matchedSeason = matchedShow.Seasons.FirstOrDefault(tSeason => tSeason.Season == (episode.ParentIndexNumber ?? -1));

                    if (matchedSeason != null)
                    {
                        // if it's not a match then it means trakt doesn't know about the episode, leave the watched state alone and move on
                        if (matchedSeason.Episodes.Contains(episode.IndexNumber ?? -1))
                        {
                            // episode is in users libary. Now we need to determine if it's watched
                            var userData = await _userDataRepository.GetUserData(user.Id, episode.GetUserDataKey()).ConfigureAwait(false);

                            var watchedShowMatch = tShowsWatched.SingleOrDefault(tShow => tShow.TvdbId == tvdbId);

                            var isWatched = false;

                            if (watchedShowMatch != null)
                            {
                                var watchedSeasonMatch = watchedShowMatch.Seasons.FirstOrDefault(tSeason => tSeason.Season == (episode.ParentIndexNumber ?? -1));

                                if (watchedSeasonMatch != null)
                                {
                                    if (watchedSeasonMatch.Episodes.Contains(episode.IndexNumber ?? -1))
                                    {
                                        userData.Played = true;
                                        isWatched = true;
                                    }
                                }
                            }

                            if (!isWatched)
                            {
                                userData.Played = false;
                                userData.PlayCount = 0;
                                userData.LastPlayedDate = null;
                            }

                            await _userDataRepository.SaveUserData(user.Id, episode.GetUserDataKey(), userData, cancellationToken);
                        }
                    }
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);

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
