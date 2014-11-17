﻿using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trakt.Api;
using Trakt.Api.DataContracts;
using Trakt.Helpers;

namespace Trakt.ScheduledTasks
{

    /// <summary>
    /// Task that will Sync each users trakt.tv profile with their local library. This task will only include 
    /// watched states.
    /// </summary>
    class SyncFromTraktTask : IScheduledTask
    {
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILogger _logger;
        private readonly TraktApi _traktApi;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="jsonSerializer"></param>
        /// <param name="userManager"></param>
        /// <param name="userDataManager"> </param>
        /// <param name="httpClient"></param>
        public SyncFromTraktTask(ILogManager logger, IJsonSerializer jsonSerializer, IUserManager userManager, IUserDataManager userDataManager, IHttpClient httpClient)
        {
            _userManager = userManager;
            _userDataManager = userDataManager;
            _logger = logger.GetLogger("Trakt");
            _traktApi = new TraktApi(jsonSerializer, _logger, httpClient);
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
                _logger.Info("No Users returned");
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

        public static bool CanSync(BaseItem item)
        {
            var movie = item as Movie;

            if (movie != null)
            {
                return !string.IsNullOrEmpty(movie.GetProviderId(MetadataProviders.Imdb)) ||
                    !string.IsNullOrEmpty(movie.GetProviderId(MetadataProviders.Tmdb));
            }

            var episode = item as Episode;

            if (episode != null && episode.Series != null && !episode.IsVirtualUnaired && !episode.IsMissingEpisode)
            {
                var series = episode.Series;
                
                return !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Imdb)) ||
                    !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Tvdb));
            }

            return false;
        }

        private async Task SyncTraktDataForUser(User user, double currentProgress, CancellationToken cancellationToken, IProgress<double> progress, double percentPerUser)
        {
            var libraryRoot = user.RootFolder;
            var traktUser = UserHelper.GetTraktUser(user);
            var syncItemFailures = 0;
            
            IEnumerable<TraktMovieDataContract> tMovies;
            IEnumerable<TraktUserLibraryShowDataContract> tShowsCollection;
            IEnumerable<TraktUserLibraryShowDataContract> tShowsWatched;

            try
            {
                /*
                 * In order to be as accurate as possible. We need to download the users show collection & the users watched shows.
                 * It's unfortunate that trakt.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
                 * like they do for movies.
                 */
                tMovies = await _traktApi.SendGetAllMoviesRequest(traktUser).ConfigureAwait(false);
                tShowsCollection = await _traktApi.SendGetCollectionShowsRequest(traktUser).ConfigureAwait(false);
                tShowsWatched = await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Exception handled", ex);
                return;
            }
            

            _logger.Info("Trakt.tv Movies count = " + tMovies.Count());
            _logger.Info("Trakt.tv ShowsCollection count = " + tShowsCollection.Count());
            _logger.Info("Trakt.tv ShowsWatched count = " + tShowsWatched.Count());

            var mediaItems = libraryRoot.GetRecursiveChildren(user)
                .Where(CanSync)
                .OrderBy(i =>
                {
                    var episode = i as Episode;

                    return episode != null ? episode.Series.Id : i.Id;
                })
                .ToList();

            // purely for progress reporting
            var percentPerItem = percentPerUser / mediaItems.Count;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                var matchedMovie = FindMatch(movie, tMovies);

                if (matchedMovie != null)
                {
                    var userData = _userDataManager.GetUserData(user.Id, movie.GetUserDataKey());

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

                    await _userDataManager.SaveUserData(user.Id, movie, userData, UserDataSaveReason.Import, cancellationToken);
                }
                else
                {
                    syncItemFailures++;
                    _logger.Info("Failed to match ", movie.Name);
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            foreach (var episode in mediaItems.OfType<Episode>())
            {
                var matchedShow = FindMatch(episode.Series, tShowsCollection);

                if (matchedShow != null)
                {
                    var matchedSeason = matchedShow.Seasons
                        .FirstOrDefault(tSeason => tSeason.Season == (episode.ParentIndexNumber ?? -1));

                    // if it's not a match then it means trakt doesn't know about the episode, leave the watched state alone and move on
                    if (matchedSeason != null && matchedSeason.Episodes.Contains(episode.IndexNumber ?? -1))
                    {
                        // episode is in users libary. Now we need to determine if it's watched
                        var userData = _userDataManager.GetUserData(user.Id, episode.GetUserDataKey());

                        var watchedShowMatch = FindMatch(episode.Series, tShowsWatched);

                        var isWatched = false;

                        if (watchedShowMatch != null)
                        {
                            var watchedSeasonMatch = watchedShowMatch.Seasons
                                .FirstOrDefault(tSeason => tSeason.Season == (episode.ParentIndexNumber ?? -1));

                            if (watchedSeasonMatch != null)
                            {
                                if (watchedSeasonMatch.Episodes.Contains(episode.IndexNumber ?? -1))
                                {
                                    //_logger.Debug("Marking as watched " + GetVerboseEpisodeData(episode));
                                    userData.Played = true;
                                    isWatched = true;
                                }
                                else
                                {
                                    syncItemFailures++;
                                    _logger.Info("No Episode match in Watched shows list " + GetVerboseEpisodeData(episode));
                                }
                            }
                            else
                            {
                                syncItemFailures++;
                                _logger.Info("No Season match in Watched shows list " + GetVerboseEpisodeData(episode));
                            }
                        }
                        else
                        {
                            syncItemFailures++;
                            _logger.Info("No Show match in Watched shows list " + GetVerboseEpisodeData(episode));
                        }

                        if (!isWatched)
                        {
                            //_logger.Debug("Marking as unwatched " + GetVerboseEpisodeData(episode));
                            userData.Played = false;
                            userData.PlayCount = 0;
                            userData.LastPlayedDate = null;
                        }

                        await _userDataManager.SaveUserData(user.Id, episode, userData, UserDataSaveReason.Import, cancellationToken);
                    }
                    else
                    {
                        syncItemFailures++;
                        _logger.Info("Failed to match episode/season numbers ", GetVerboseEpisodeData(episode));
                    }
                }
                else
                {
                    syncItemFailures++;
                    _logger.Info("Failed to match show " + GetVerboseEpisodeData(episode));
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);                
            }
            //_logger.Info(syncItemFailures + " items not parsed");
        }

        private string GetVerboseEpisodeData(Episode episode)
        {
            string episodeString = "";
            episodeString += "Episode: " + (episode.ParentIndexNumber != null ? episode.ParentIndexNumber.ToString() : "null");
            episodeString += "x" + (episode.IndexNumber != null ? episode.IndexNumber.ToString() : "null");
            episodeString += " '" + episode.Name + "' ";
            episodeString += "Series: '" + (episode.Series != null
                ? !String.IsNullOrWhiteSpace(episode.Series.Name)
                    ? episode.Series.Name
                    : "null property"
                : "null class");
            episodeString += "'";

            return episodeString;
        }

        public static TraktUserLibraryShowDataContract FindMatch(Series item, IEnumerable<TraktUserLibraryShowDataContract> results)
        {
            return results.FirstOrDefault(i =>
            {
                var imdb = item.GetProviderId(MetadataProviders.Imdb);

                if (!string.IsNullOrWhiteSpace(imdb) &&
                    string.Equals(imdb, i.ImdbId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var tvdb = item.GetProviderId(MetadataProviders.Tvdb);
                if (!string.IsNullOrWhiteSpace(tvdb) &&
                    string.Equals(imdb, i.TvdbId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            });
        }

        public static TraktMovieDataContract FindMatch(BaseItem item, IEnumerable<TraktMovieDataContract> results)
        {
            return results.FirstOrDefault(i =>
            {
                var imdb = item.GetProviderId(MetadataProviders.Imdb);

                if (!string.IsNullOrWhiteSpace(imdb) && 
                    string.Equals(imdb, i.ImdbId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var tmdb = item.GetProviderId(MetadataProviders.Tmdb);
                if (!string.IsNullOrWhiteSpace(tmdb) &&
                    string.Equals(imdb, i.TmdbId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            });
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
            get { return "Sync Watched/Unwatched status from Trakt.tv for each MB3 user that has a configured Trakt account"; }
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
