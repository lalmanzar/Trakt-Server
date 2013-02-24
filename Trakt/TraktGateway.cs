using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Threading.Tasks;
using MediaBrowser.Model.Serialization;
using Trakt.Api;
using Trakt.Model;

namespace Trakt
{
    /// <summary>
    /// This class will contain the various methods that MB3 server will call in order to communicate with the trakt.tv site
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
        public static async Task SendWatchingState(Video video, TraktUser traktUser, IJsonSerializer jsonSerializer)
        {
            if (video is Movie)
            {
                await TraktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Watching, traktUser, jsonSerializer).ConfigureAwait(false);
            }
            else if (video is Episode)
            {
                await TraktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Watching, traktUser, jsonSerializer).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// Called when a video has been played past the MaxResumePercentage. Lets trakt.tv know to mark
        /// an item as watched.
        /// </summary>
        /// <param name="video"> The video that has just finished</param>
        /// <param name="traktUser">The user who's finished watching the video</param>
        /// <returns></returns>
        public static async Task SendScrobbleState(Video video, TraktUser traktUser, IJsonSerializer jsonSerializer)
        {
            if (video is Movie)
            {
                await TraktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Scrobble, traktUser, jsonSerializer).ConfigureAwait(false);
            }
            else if (video is Episode)
            {
                await TraktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Scrobble, traktUser, jsonSerializer).ConfigureAwait(false);
            }
        }
        
    }
}
