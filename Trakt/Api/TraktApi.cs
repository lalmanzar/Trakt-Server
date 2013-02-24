using System;
using MediaBrowser.Common.Serialization;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
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
        public static Task<TraktResponseDataContract> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, TraktUser traktUser)
        {
            return SendMovieStatusUpdate(movie, mediaStatus, traktUser);
        }

        private static async Task<TraktResponseDataContract> SendMovieStatusUpdate(Movie movie, MediaStatus status, TraktUser traktUser)
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

            return JsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }


                
        /// <summary>
        /// Reports to trakt.tv that an episode is being watched, or has been watched.
        /// </summary>
        /// <param name="episode">The episode being watched</param>
        /// <param name="status">Enum indicating whether an episode is being watched or scrobbled</param>
        /// <param name="traktUser">The user that's watching the episode</param>
        /// <returns>A standard TraktResponseDTO</returns>
        public static Task<TraktResponseDataContract> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, TraktUser traktUser)
        {
            return SendEpisodeStatusUpdate(episode, status, traktUser);
        }

        private static async Task<TraktResponseDataContract> SendEpisodeStatusUpdate(Episode episode, MediaStatus status, TraktUser traktUser)
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

            return JsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Add a list of movies to the users trakt.tv library
        /// </summary>
        /// <param name="movies">The movies to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> SendLibraryUpdateAsync(IEnumerable<Movie> movies, TraktUser traktUser)
        {
            var moviesPayload = new List<object>();

            foreach (Movie m in movies)
            {
                var id = "";

                try
                {
                    id = m.ProviderIds["Imdb"];
                }
                catch (Exception)
                {
                    //logger.LogInfo("Imdb Id not found for '" + m.Name + "'", null);
                }

                var movieData = new
                {
                    title = m.Name,
                    imdb_id = id,
                    year = m.ProductionYear ?? 0
                };

                moviesPayload.Add(movieData);
            }

            var data = new Dictionary<string, string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            data.Add("movies", JsonSerializer.SerializeToString(moviesPayload));

            try
            {
                Stream response =
                await
                Kernel.Instance.HttpManager.Post(TraktUris.MovieLibrary, data, Kernel.Instance.ResourcePools.Trakt,
                                                                     System.Threading.CancellationToken.None).ConfigureAwait(false);

                return JsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
            }
            catch (Exception e)
            {
                return new TraktResponseDataContract {Error = e.Message};
            }
            
        }



        /// <summary>
        /// Add a list of Episodes to the users trakt.tv library
        /// </summary>
        /// <param name="episodes">The episodes to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> SendLibraryUpdateAsync(IReadOnlyList<Episode> episodes, TraktUser traktUser)
        {
            var episodesPayload = new List<object>();

            foreach (Episode ep in episodes)
            {
                var episodeData = new
                {
                    season = ep.ParentIndexNumber,
                    episode = ep.IndexNumber
                };

                episodesPayload.Add(episodeData);
            }

            var data = new Dictionary<string, string>();
            
            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            try
            {
                data.Add("imdb_id", episodes[0].Series.ProviderIds["Imdb"]);
            }
            catch(Exception)
            {
                //logger.LogInfo("Imdb Id not found for '" + episodes[0].Series.Name + "'", null);
            }
            try
            {
                data.Add("tvdb_id", episodes[0].Series.ProviderIds["Tvdb"]);
            }
            catch (Exception)
            {

                //logger.LogInfo("Tvdb Id not found for '" + episodes[0].Series.Name + "'", null);
            }
            data.Add("title", episodes[0].Series.Name);
            data.Add("year", (episodes[0].Series.ProductionYear ?? 0).ToString());    
            data.Add("episodes", JsonSerializer.SerializeToString(episodesPayload));

            try
            {
                Stream response =
                await
                Kernel.Instance.HttpManager.Post(TraktUris.ShowEpisodeLibrary, data, Kernel.Instance.ResourcePools.Trakt,
                                                 System.Threading.CancellationToken.None).ConfigureAwait(false);

                return JsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
            }
            catch (Exception e)
            {
                return new TraktResponseDataContract {Error = e.Message};
            }
            
        }
    }
}
