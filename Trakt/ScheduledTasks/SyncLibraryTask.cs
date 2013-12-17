using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Trakt.Api;
using Trakt.Helpers;

namespace Trakt.ScheduledTasks
{
    /// <summary>
    /// Task that will Sync each users local library with their respective trakt.tv profiles. This task will only include 
    /// titles, watched states will be synced in other tasks.
    /// </summary>
    [Export(typeof(IScheduledTask))]
    public class SyncLibraryTask : IScheduledTask
    {
        //private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IUserManager _userManager;
        private readonly ILogger _logger;
        private TraktApi traktApi;

        public SyncLibraryTask(ILogManager logger, IJsonSerializer jsonSerializer, IUserManager userManager)
        {
            _jsonSerializer = jsonSerializer;
            _userManager = userManager;
            _logger = logger.GetLogger("Trakt");
            traktApi = new TraktApi(_jsonSerializer, _logger);
        }

        public IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {
            return new List<ITaskTrigger>();
        }

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
            var progPercent = 0.0;
            var percentPerUser = 100 / users.Count;

            foreach (var user in users)
            {
                var libraryRoot = user.RootFolder;
                var traktUser = UserHelper.GetTraktUser(user);

                var movies = new List<Movie>();
                var episodes = new List<Episode>();
                var currentSeriesId = Guid.Empty;
                
                var mediaItems = libraryRoot.GetRecursiveChildren(user)
                    .Where(i => i.Name != null &&
                        (i is Episode && !string.IsNullOrEmpty(((Episode)i).Series.GetProviderId(MetadataProviders.Tvdb))) || 
                        (i is Movie && !string.IsNullOrEmpty(i.GetProviderId(MetadataProviders.Imdb))))
                    .OrderBy(i =>
                    {
                        var episode = i as Episode;

                        return episode != null ? episode.Series.Id : i.Id;
                    })
                    .ToList();

                if (mediaItems.Count == 0) continue;

                // purely for progress reporting
                var percentPerItem = (double) percentPerUser / (double) mediaItems.Count;

                foreach (var child in mediaItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (child.Path == null || child.LocationType == LocationType.Virtual) continue;

                    foreach (var s in traktUser.TraktLocations.Where(s => child.Path.StartsWith(s + "\\")))
                    {
                        if (child is Movie)
                        {
                            movies.Add(child as Movie);

                            // publish if the list hits a certain size
                            if (movies.Count >= 200)
                            {
                                try
                                {
                                    await traktApi.SendLibraryUpdateAsync(movies, traktUser, cancellationToken, EventType.Add).ConfigureAwait(false);
                                }
                                catch (ArgumentNullException argNullEx)
                                {
                                    _logger.ErrorException("ArgumentNullException handled sending movies to trakt.tv", argNullEx);
                                }
                                catch (Exception e)
                                {
                                    _logger.ErrorException("Exception handled sending movies to trakt.tv", e);
                                }
                                movies.Clear();
                            }
                        }
                        else if (child is Episode)
                        {
                            var ep = child as Episode;

                            if (currentSeriesId != ep.Series.Id && episodes.Count > 0)
                            {
                                // We're starting a new show. Finish up with the old one
                                try
                                {
                                    await traktApi.SendLibraryUpdateAsync(episodes, traktUser, cancellationToken, EventType.Add).ConfigureAwait(false);
                                }
                                catch (ArgumentNullException argNullEx)
                                {
                                    _logger.ErrorException("ArgumentNullException handled sending episodes to trakt.tv", argNullEx);
                                }
                                catch (Exception e)
                                {
                                    _logger.ErrorException("Exception handled sending episodes to trakt.tv", e);
                                }
                                
                                episodes.Clear();
                            }

                            currentSeriesId = ep.Series.Id;
                            episodes.Add(ep);
                        }
                    }

                    // purely for progress reporting
                    progPercent += percentPerItem;
                    progress.Report(progPercent);
                }

                // send any remaining entries
                if (movies.Count > 0)
                {
                    try
                    {
                        await traktApi.SendLibraryUpdateAsync(movies, traktUser, cancellationToken, EventType.Add).ConfigureAwait(false);
                    }
                    catch (ArgumentNullException argNullEx)
                    {
                        _logger.ErrorException("ArgumentNullException handled sending movies to trakt.tv", argNullEx);
                    }
                    catch (Exception e)
                    {
                        _logger.ErrorException("Exception handled sending movies to trakt.tv", e);
                    }
                    
                }

                if (episodes.Count > 0)
                {
                    try
                    {
                        await traktApi.SendLibraryUpdateAsync(episodes, traktUser, cancellationToken, EventType.Add).ConfigureAwait(false);
                    }
                    catch (ArgumentNullException argNullEx)
                    {
                        _logger.ErrorException("ArgumentNullException handled sending episodes to trakt.tv", argNullEx);
                    }
                    catch (Exception e)
                    {
                        _logger.ErrorException("Exception handled sending episodes to trakt.tv", e);
                    }
                }
            }
        }

        public string Name
        {
            get { return "Sync library to trakt.tv"; }
        }

        public string Category
        {
            get
            {
                return "Trakt";
            }
        }

        public string Description
        {
            get
            {
                return
                    "Adds any media that is in each users trakt monitored locations to their trakt.tv profile";
            }
        }
    }
}
