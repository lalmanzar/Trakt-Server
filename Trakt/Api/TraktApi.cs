using MediaBrowser.Common.Serialization;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Trakt.Api.DataContracts;
using Trakt.Model;

namespace Trakt.Api
{
    /// <summary>
    /// This class contains the actual api calls. These methods should not be called directly. Instead make all plugin calls to methods contained in TraktGateway
    /// </summary>
    public static class TraktApi
    {

        /// <summary>
        /// Report to trakt.tv that a movie is being watched, or has been watched.
        /// </summary>
        /// <param name="movie">The movie being watched/scrobbled</param>
        /// <param name="mediaStatus">MediaStatus enum dictating whether item is being watched or scrobbled</param>
        /// <param name="traktUser">The user that watching the current movie</param>
        /// <returns>A standard TraktResponseDTO</returns>
        public static Task<TraktResponseDto> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, TraktUser traktUser)
        {
            return SendMovieStatusUpdate(movie, mediaStatus, traktUser);
        }

        private static async Task<TraktResponseDto> SendMovieStatusUpdate(Movie movie, MediaStatus status, TraktUser traktUser)
        {
            Dictionary<string, string> data = new Dictionary<string,string>();
            
            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            data.Add("imdb_id", movie.ProviderIds["Imdb"]);
            data.Add("tmdb_id", movie.ProviderIds["Tmdb"]);
            data.Add("title", movie.Name);
            data.Add("year", movie.ProductionYear.ToString());
            data.Add("duration", ((int)((movie.RunTimeTicks / 10000000) / 60)).ToString());


            Stream response = null;

            if (status == MediaStatus.Watching)
                response = await Kernel.Instance.HttpManager.Post(TraktUris.MovieWatching, data, Kernel.Instance.ResourcePools.Trakt, System.Threading.CancellationToken.None).ConfigureAwait(false);
            else if (status == MediaStatus.Scrobble)
                response = await Kernel.Instance.HttpManager.Post(TraktUris.MovieScrobble, data, Kernel.Instance.ResourcePools.Trakt, System.Threading.CancellationToken.None).ConfigureAwait(false);

            return JsonSerializer.DeserializeFromStream<TraktResponseDto>(response);
        }


                
        /// <summary>
        /// Reports to trakt.tv that an episode is being watched, or has been watched.
        /// </summary>
        /// <param name="episode">The episode being watched</param>
        /// <param name="status">Enum indicating whether an episode is being watched or scrobbled</param>
        /// <param name="traktUser">The user that's watching the episode</param>
        /// <returns>A standard TraktResponseDTO</returns>
        public static Task<TraktResponseDto> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, TraktUser traktUser)
        {
            return SendEpisodeStatusUpdate(episode, status, traktUser);
        }

        private static async Task<TraktResponseDto> SendEpisodeStatusUpdate(Episode episode, MediaStatus status, TraktUser traktUser)
        {
            Dictionary<string, string> data = new Dictionary<string,string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            data.Add("imdb_id", episode.ProviderIds["Imdb"]);
            data.Add("tvdb_id", episode.ProviderIds["Tvdb"]);
            data.Add("title", episode.Name);
            data.Add("year", episode.ProductionYear.ToString());
            data.Add("season", episode.Season.IndexNumber.ToString());
            data.Add("episode", episode.IndexNumber.ToString());
            data.Add("duration", ((int)((episode.RunTimeTicks / 10000000) / 60)).ToString());

            Stream response = null;

            if (status == MediaStatus.Watching)
                response = await Kernel.Instance.HttpManager.Post(TraktUris.ShowWatching, data, Kernel.Instance.ResourcePools.Trakt, System.Threading.CancellationToken.None).ConfigureAwait(false);
            else if (status == MediaStatus.Scrobble)
                response = await Kernel.Instance.HttpManager.Post(TraktUris.ShowScrobble, data, Kernel.Instance.ResourcePools.Trakt, System.Threading.CancellationToken.None).ConfigureAwait(false);

            return JsonSerializer.DeserializeFromStream<TraktResponseDto>(response);
        }
    }
}
