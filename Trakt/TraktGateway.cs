using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Threading.Tasks;
using Trakt.Api;
using Trakt.Model;

namespace Trakt
{
    /// <summary>
    /// This class will contain the various methods that the app will call in order to communicate with the trakt.tv site
    /// </summary>
    public static class TraktGateway
    {
        /// <summary>
        /// Called when playback starts to let trakt.tv know a user is watching something.
        /// Also called periodically during playback so that trakt.tv continues to show 'watching' state
        /// </summary>
        /// <param name="video">The video that the user has started watching</param>
        /// <param name="traktUser">The user who's watching the video</param>
        /// <returns></returns>
        public static async Task SendWatchingState(Video video, TraktUser traktUser)
        {
            if (video is Movie)
            {
                await TraktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Watching, traktUser).ConfigureAwait(false);
            }
            else if (video is Episode)
            {
                await TraktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Watching, traktUser).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// Called when a video has been played past the MaxResumePercentage. Lets trakt.tv know to mark
        /// an item as watched.
        /// </summary>
        /// <param name="video"> The video that has just finished</param>
        /// <param name="traktUser">The user who's finished watching the video</param>
        /// <returns></returns>
        public static async Task SendScrobbleState(Video video, TraktUser traktUser)
        {
            if (video is Movie)
            {
                await TraktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Scrobble, traktUser).ConfigureAwait(false);
            }
            else if (video is Episode)
            {
                await TraktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Scrobble, traktUser).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// Adds all the media titles that are in trakt locations to the users trakt.tv library
        /// </summary>
        /// <param name="user">The user who's library is being updated</param>
        /// <returns></returns>
        public static async Task SyncMediaToTraktTv(User user)
        {
            var libaryRoot = user.RootFolder;
            var traktUser = UserHelper.GetTraktUser(user);

            if (traktUser == null || libaryRoot == null || traktUser.TraktLocations == null) return;

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
                        if (movies.Count == 200)
                        {
                            await TraktApi.SendLibraryUpdateAsync(movies, traktUser).ConfigureAwait(false);
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
                            await TraktApi.SendLibraryUpdateAsync(episodes, traktUser).ConfigureAwait(false);
                            episodes.Clear();

                            episodes.Add(ep);
                        }
                    }
                }
            }

            // send any remaining entries
            if (movies.Count > 0)
                await TraktApi.SendLibraryUpdateAsync(movies, traktUser).ConfigureAwait(false);

            if (episodes.Count > 0)
                await TraktApi.SendLibraryUpdateAsync(episodes, traktUser).ConfigureAwait(false);
        }
    }
}
