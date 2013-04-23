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
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Trakt.Api;

namespace Trakt.ScheduledTasks
{
    /// <summary>
    /// Task that will Sync each users local library with their respective trakt.tv profiles. This task will only include 
    /// titles, watched states will be synced in other tasks.
    /// </summary>
    [Export(typeof(IScheduledTask))]
    public class SyncLibraryTask : IScheduledTask
    {
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly Kernel _kernel;
        private readonly IUserManager _userManager;
        private TraktApi traktApi;

        public SyncLibraryTask(Kernel kernel, ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer, IUserManager userManager)
        {
            _kernel = kernel;
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _userManager = userManager;
            traktApi = new TraktApi(_httpClient, _jsonSerializer, logger);
        }

        public IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {
            return new List<ITaskTrigger>();
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            // purely for progress reporting
            var progPercent = 0.0;
            var percentPerUser = 100/_userManager.Users.Count();

            foreach (var user in _userManager.Users)
            {
                var libraryRoot = user.RootFolder;
                var traktUser = UserHelper.GetTraktUser(user);

                if (traktUser == null || traktUser.TraktLocations == null)
                {
                    progPercent += percentPerUser;
                    progress.Report(progPercent);
                    continue;
                }

                var movies = new List<Movie>();
                var episodes = new List<Episode>();
                var currentShow = "";

                var mediaItems = libraryRoot.GetRecursiveChildren(user)
                    .Where(i => i is Episode || i is Movie)
                    .ToList();

                if (mediaItems.Count == 0) continue;

                // purely for progress reporting
                var percentPerItem = (double) percentPerUser / (double) mediaItems.Count;

                foreach (var child in mediaItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (child.Path == null) continue;

                    foreach (var s in traktUser.TraktLocations.Where(s => child.Path.StartsWith(s + "\\")))
                    {
                        if (child is Movie)
                        {
                            movies.Add(child as Movie);

                            // publish if the list hits a certain size
                            if (movies.Count >= 200)
                            {
                                await traktApi.SendLibraryUpdateAsync(movies, traktUser, cancellationToken).ConfigureAwait(false);
                                movies.Clear();
                            }
                        }
                        else if (child is Episode)
                        {
                            var ep = child as Episode;

                            if (string.IsNullOrEmpty(currentShow)) currentShow = ep.Series.Name;

                            if (currentShow.Equals(ep.Series.Name))
                            {
                                episodes.Add(ep);
                            }
                            else
                            {
                                // We're starting a new show. Finish up with the old one
                                await traktApi.SendLibraryUpdateAsync(episodes, traktUser, cancellationToken).ConfigureAwait(false);
                                episodes.Clear();

                                episodes.Add(ep);
                            }
                        }
                    }

                    // purely for progress reporting
                    progPercent += percentPerItem;
                    progress.Report(progPercent);
                }

                // send any remaining entries
                if (movies.Count > 0)
                    await traktApi.SendLibraryUpdateAsync(movies, traktUser, cancellationToken).ConfigureAwait(false);

                if (episodes.Count > 0)
                    await traktApi.SendLibraryUpdateAsync(episodes, traktUser, cancellationToken).ConfigureAwait(false);
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
