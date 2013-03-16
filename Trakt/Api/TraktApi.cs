using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Model.Serialization;
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
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <param name="httpClient"></param>
        /// <param name="jsonSerializer"></param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> AccountTest(TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            var data = new Dictionary<string, string> {{"username", traktUser.UserName}, {"password", traktUser.PasswordHash}};

            Stream response =
                await
                httpClient.Post(TraktUris.AccountTest, data, Plugin.Instance.TraktResourcePool,
                                                                     System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <param name="httpClient"></param>
        /// <param name="jsonSerializer"></param>
        /// <returns></returns>
        public static async Task<AccountSettingsDataContract> GetUserAccount(TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            Stream response =
                await
                httpClient.Post(TraktUris.AccountTest, data, Plugin.Instance.TraktResourcePool,
                                                                     System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<AccountSettingsDataContract>(response);
        }



        /// <summary>
        /// Report to trakt.tv that a movie is being watched, or has been watched.
        /// </summary>
        /// <param name="movie">The movie being watched/scrobbled</param>
        /// <param name="mediaStatus">MediaStatus enum dictating whether item is being watched or scrobbled</param>
        /// <param name="traktUser">The user that watching the current movie</param>
        /// <param name="httpClient"> </param>
        /// <param name="jsonSerializer"> </param>
        /// <returns>A standard TraktResponseDTO</returns>
        public static async Task<TraktResponseDataContract> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
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

            if (mediaStatus == MediaStatus.Watching)
                response = await httpClient.Post(TraktUris.MovieWatching, data, Plugin.Instance.TraktResourcePool, System.Threading.CancellationToken.None).ConfigureAwait(false);
            else if (mediaStatus == MediaStatus.Scrobble)
                response = await httpClient.Post(TraktUris.MovieScrobble, data, Plugin.Instance.TraktResourcePool, System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Reports to trakt.tv that an episode is being watched, or has been watched.
        /// </summary>
        /// <param name="episode">The episode being watched</param>
        /// <param name="status">Enum indicating whether an episode is being watched or scrobbled</param>
        /// <param name="traktUser">The user that's watching the episode</param>
        /// <param name="httpClient"> </param>
        /// <param name="jsonSerializer"> </param>
        /// <returns>A standard TraktResponseDTO</returns>
        public static async Task<TraktResponseDataContract> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
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
                response = await httpClient.Post(TraktUris.ShowWatching, data, Plugin.Instance.TraktResourcePool, System.Threading.CancellationToken.None).ConfigureAwait(false);
            else if (status == MediaStatus.Scrobble)
                response = await httpClient.Post(TraktUris.ShowScrobble, data, Plugin.Instance.TraktResourcePool, System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Add a list of movies to the users trakt.tv library
        /// </summary>
        /// <param name="movies">The movies to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="httpClient"> </param>
        /// <param name="jsonSerializer"> </param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> SendLibraryUpdateAsync(List<Movie> movies, TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            var moviesPayload = new List<object>();

            foreach (Movie m in movies)
            {
                var movieData = new
                {
                    title = m.Name,
                    imdb_id = m.ProviderIds["Imdb"],
                    year = m.ProductionYear ?? 0
                };

                moviesPayload.Add(movieData);
            }

            var data = new Dictionary<string, string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            data.Add("movies", jsonSerializer.SerializeToString(moviesPayload));

            Stream response =
                await
                httpClient.Post(TraktUris.MovieLibrary, data, Plugin.Instance.TraktResourcePool,
                                                                     System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Add a list of Episodes to the users trakt.tv library
        /// </summary>
        /// <param name="episodes">The episodes to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="httpClient"> </param>
        /// <param name="jsonSerializer"> </param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> SendLibraryUpdateAsync(IReadOnlyList<Episode> episodes, TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
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
            data.Add("imdb_id", episodes[0].Series.ProviderIds["Imdb"]);
            data.Add("tvdb_id", episodes[0].Series.ProviderIds["Tvdb"]);
            data.Add("title", episodes[0].Series.Name);
            data.Add("year", (episodes[0].Series.ProductionYear ?? 0).ToString());
            data.Add("episodes", jsonSerializer.SerializeToString(episodesPayload));

            Stream response =
                await
                httpClient.Post(TraktUris.ShowEpisodeLibrary, data, Plugin.Instance.TraktResourcePool,
                                                 System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Rate an item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rating"></param>
        /// <param name="traktUser"></param>
        /// <param name="httpClient"></param>
        /// <param name="jsonSerializer"></param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> SendItemRating(BaseItem item, string rating, TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            string url = "";
            var data = new Dictionary<string, string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            data.Add("imdb_id", item.ProviderIds["Imdb"]);

            if (item is Movie)
            {
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                url = TraktUris.RateMovie;
            }
            else if (item is Episode)
            {
                data.Add("title", ((Episode)item).Series.Name);
                data.Add("year", ((Episode)item).Series.ProductionYear != null ? ((Episode)item).Series.ProductionYear.ToString() : "");
                data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
                data.Add("season", ((Episode)item).Season.IndexNumber.ToString());
                data.Add("episode", ((Episode)item).IndexNumber.ToString());
                url = TraktUris.RateEpisode;
            }
            else // It's a Series
            {
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
                url = TraktUris.RateShow;
            }

            data.Add("rating", rating);

            Stream response =
                await
                httpClient.Post(url, data, Plugin.Instance.TraktResourcePool,
                                                 System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="comment"></param>
        /// <param name="containsSpoilers"></param>
        /// <param name="traktUser"></param>
        /// <param name="httpClient"></param>
        /// <param name="jsonSerializer"></param>
        /// <returns></returns>
        public static async Task<TraktResponseDataContract> SendItemComment(BaseItem item, string comment, bool containsSpoilers, TraktUser traktUser, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            string url = "";
            var data = new Dictionary<string, string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            data.Add("imdb_id", item.ProviderIds["Imdb"]);

            if (item is Movie)
            {
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                url = TraktUris.CommentMovie;
            }
            else if (item is Episode)
            {
                data.Add("title", ((Episode)item).Series.Name);
                data.Add("year", ((Episode)item).Series.ProductionYear != null ? ((Episode)item).Series.ProductionYear.ToString() : "");
                data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
                data.Add("season", ((Episode)item).Season.IndexNumber.ToString());
                data.Add("episode", ((Episode)item).IndexNumber.ToString());
                url = TraktUris.CommentEpisode;   
            }
            else // It's a Series
            {
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
                url = TraktUris.CommentShow;
            }

            data.Add("comment", comment);
            data.Add("spoiler", containsSpoilers.ToString());
            data.Add("review", "false");

            Stream response =
                await
                httpClient.Post(url, data, Plugin.Instance.TraktResourcePool,
                                                 System.Threading.CancellationToken.None).ConfigureAwait(false);

            return jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }
    }
}
