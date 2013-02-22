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
using MediaBrowser.Model.Tasks;
using Trakt.Api;

namespace Trakt.ScheduledTasks
{
    /// <summary>
    /// Task that will Sync each users local library with their respective trakt.tv profiles. This task will only include 
    /// titles, watched states will be synced in other tasks.
    /// </summary>
    [Export(typeof(IScheduledTask))]
    public class SyncLibraryTask : BaseScheduledTask<Kernel>
    {
        protected override IEnumerable<BaseTaskTrigger> GetDefaultTriggers()
        {
            return new List<BaseTaskTrigger>() ;
        }

        protected override async Task ExecuteInternal(CancellationToken cancellationToken, IProgress<TaskProgress> progress)
        {
            progress.Report(new TaskProgress { PercentComplete = 0 });
            
            var processedItemCount = 0;
            var currentPercent = 0;
            var maxPercentPerUser = 100/Kernel.Users.Count();
            
            foreach (var user in Kernel.Users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var libraryRoot = user.RootFolder;
                var traktUser = UserHelper.GetTraktUser(user);

                if (traktUser == null || libraryRoot == null || traktUser.TraktLocations == null) continue;

                // Used for progress reporting
                var itemsPerPercent = libraryRoot.RecursiveChildren.Count()/maxPercentPerUser;

                var movies = new List<Movie>();
                var episodes = new List<Episode>();
                var currentShow = "";

                foreach (var child in libraryRoot.RecursiveChildren.OfType<Video>())
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
                                TraktApi.SendLibraryUpdateAsync(movies, traktUser).ConfigureAwait(false);
                                
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
                                TraktApi.SendLibraryUpdateAsync(episodes, traktUser).ConfigureAwait(false);
                                
                                episodes.Clear();

                                episodes.Add(ep);
                            }
                        }
                    }

                    processedItemCount += 1;

                    if (processedItemCount == itemsPerPercent)
                    {
                        currentPercent += 1;
                        processedItemCount = 0;
                        progress.Report(new TaskProgress { PercentComplete = currentPercent <= 100 ? currentPercent : 100 });
                    }

                }

                // send any remaining entries
                if (movies.Count > 0)
                {
                    TraktApi.SendLibraryUpdateAsync(movies, traktUser).ConfigureAwait(false);
                }

                if (episodes.Count > 0)
                {
                    TraktApi.SendLibraryUpdateAsync(episodes, traktUser).ConfigureAwait(false);
                }
            }

            
            progress.Report(new TaskProgress { PercentComplete = 100 });
        }

        public override string Name
        {
            get { return "Sync library to trakt.tv"; }
        }

        public override string Category
        {
            get
            {
                return "Trakt";
            }
        }

        public override string Description
        {
            get
            {
                return
                    "Adds any media that is in each users trakt monitored locations to their trakt.tv profile";
            }
        }
    }
}
