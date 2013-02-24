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
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
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
        private readonly IJsonSerializer _jsonSerializer;

        public SyncLibraryTask(Kernel kernel, ITaskManager taskManager, ILogger logger, IJsonSerializer jsonSerializer) : base(kernel, taskManager, logger)
        {
            _jsonSerializer = jsonSerializer;
        }

        public override IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {
            return new List<ITaskTrigger>();
        }

        protected override async Task ExecuteInternal(CancellationToken cancellationToken, IProgress<double> progress)
        {
            foreach (var user in Kernel.Users)
            {
                var libaryRoot = user.RootFolder;
                var traktUser = UserHelper.GetTraktUser(user);

                if (traktUser == null || libaryRoot == null || traktUser.TraktLocations == null) continue;

                var movies = new List<Movie>();
                var episodes = new List<Episode>();
                var currentShow = "";

                foreach (var child in libaryRoot.RecursiveChildren)
                {
                    if (child.Path == null) continue;

                    foreach (var s in traktUser.TraktLocations.Where(s => child.Path.StartsWith(s + "\\")))
                    {
                        if (child is Movie)
                        {
                            movies.Add(child as Movie);

                            // publish if the list hits a certain size
                            if (movies.Count >= 200)
                            {
                                await TraktApi.SendLibraryUpdateAsync(movies, traktUser, _jsonSerializer).ConfigureAwait(false);
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
                                await TraktApi.SendLibraryUpdateAsync(episodes, traktUser, _jsonSerializer).ConfigureAwait(false);
                                episodes.Clear();

                                episodes.Add(ep);
                            }
                        }
                    }
                }

                // send any remaining entries
                if (movies.Count > 0)
                    await TraktApi.SendLibraryUpdateAsync(movies, traktUser, _jsonSerializer).ConfigureAwait(false);

                if (episodes.Count > 0)
                    await TraktApi.SendLibraryUpdateAsync(episodes, traktUser, _jsonSerializer).ConfigureAwait(false);
            }
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
