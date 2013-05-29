using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Trakt.Api.DataContracts;
using Trakt.Helpers;
using Trakt.Model;
using MediaBrowser.Model.Entities;
using Trakt.Net;

namespace Trakt.Api
{
    /// <summary>
    /// 
    /// </summary>
    public class TraktApi
    {
        private readonly HttpClientManager _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;

        public TraktApi(IJsonSerializer jsonSerializer, ILogger logger)
        {
            _httpClient = HttpClientManager.Instance;
            _jsonSerializer = jsonSerializer;
            _logger = logger;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<TraktResponseDataContract> AccountTest(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> {{"username", traktUser.UserName}, {"password", traktUser.PasswordHash}};

            var response =
                await
                _httpClient.Post(TraktUris.AccountTest, data, Plugin.Instance.TraktResourcePool,
                                                                     CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<AccountSettingsDataContract> GetUserAccount(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            var response =
                await
                _httpClient.Post(TraktUris.AccountTest, data, Plugin.Instance.TraktResourcePool,
                                                                     CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<AccountSettingsDataContract>(response);
        }



        /// <summary>
        /// Return a list of the users friends
        /// </summary>
        /// <param name="traktUser">The user who's friends you want to retrieve</param>
        /// <returns>A TraktFriendDataContract</returns>
        public async Task<TraktFriendDataContract> GetUserFriends(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            var response = await _httpClient.Post(string.Format(TraktUris.Friends, traktUser.UserName), data, Plugin.Instance.TraktResourcePool,
                                                                     CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<TraktFriendDataContract>(response);
            
        }



        /// <summary>
        /// Report to trakt.tv that a movie is being watched, or has been watched.
        /// </summary>
        /// <param name="movie">The movie being watched/scrobbled</param>
        /// <param name="mediaStatus">MediaStatus enum dictating whether item is being watched or scrobbled</param>
        /// <param name="traktUser">The user that watching the current movie</param>
        /// <returns>A standard TraktResponse Data Contract</returns>
        public async Task<TraktResponseDataContract> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, TraktUser traktUser)
        {
            var data = new Dictionary<string,string>();
            
            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);

            data.Add("imdb_id", movie.GetProviderId(MetadataProviders.Imdb));

            try
            {
                data.Add("tmdb_id", movie.ProviderIds["Tmdb"]);
            }
            catch (Exception)
            {}
            data.Add("title", movie.Name);
            data.Add("year", movie.ProductionYear != null ? movie.ProductionYear.ToString() : "");
            data.Add("duration", movie.RunTimeTicks != null ? ((int)((movie.RunTimeTicks / 10000000) / 60)).ToString(CultureInfo.InvariantCulture) : "");


            Stream response = null;

            if (mediaStatus == MediaStatus.Watching)
                response = await _httpClient.Post(TraktUris.MovieWatching, data, Plugin.Instance.TraktResourcePool, CancellationToken.None).ConfigureAwait(false);
            else if (mediaStatus == MediaStatus.Scrobble)
                response = await _httpClient.Post(TraktUris.MovieScrobble, data, Plugin.Instance.TraktResourcePool, CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Reports to trakt.tv that an episode is being watched, or has been watched.
        /// </summary>
        /// <param name="episode">The episode being watched</param>
        /// <param name="status">Enum indicating whether an episode is being watched or scrobbled</param>
        /// <param name="traktUser">The user that's watching the episode</param>
        /// <returns>A standard TraktResponse Data Contract</returns>
        public async Task<TraktResponseDataContract> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, TraktUser traktUser)
        {
            var data = new Dictionary<string,string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            try 
            {
                data.Add("imdb_id", episode.Series.ProviderIds["Imdb"]);
            }
            catch(Exception)
            {}
            try
            {
                data.Add("tvdb_id", episode.Series.ProviderIds["Tvdb"]);
            }
            catch(Exception)
            {}

            if (episode.Series == null || episode.Season == null)
                 return null;

            data.Add("title", episode.Series.Name);
            data.Add("year", episode.Series.ProductionYear != null ? episode.Series.ProductionYear.ToString() : "");
            data.Add("season", episode.Season.IndexNumber != null ? episode.Season.IndexNumber.ToString() : "");
            data.Add("episode", episode.IndexNumber != null ? episode.IndexNumber.ToString() : "");
            data.Add("duration", episode.RunTimeTicks != null ? ((int)((episode.RunTimeTicks / 10000000) / 60)).ToString(CultureInfo.InvariantCulture) : "");

            Stream response = null;

            if (status == MediaStatus.Watching)
                response = await _httpClient.Post(TraktUris.ShowWatching, data, Plugin.Instance.TraktResourcePool, CancellationToken.None).ConfigureAwait(false);
            else if (status == MediaStatus.Scrobble)
                response = await _httpClient.Post(TraktUris.ShowScrobble, data, Plugin.Instance.TraktResourcePool, CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }


        /// <summary>
        /// Add or remove a list of movies to/from the users trakt.tv library
        /// </summary>
        /// <param name="movies">The movies to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{TraktResponseDataContract}.</returns>
        public async Task<TraktResponseDataContract> SendLibraryUpdateAsync(List<Movie> movies, TraktUser traktUser, CancellationToken cancellationToken, EventType eventType)
        {
            if (movies == null)
                throw new ArgumentNullException("movies");
            if (traktUser == null)
                throw new ArgumentNullException("traktUser");

            if (eventType == EventType.Update) return null;

            var moviesPayload = new List<object>();

            foreach (var m in movies)
            {
                var movieData = new
                {
                    title = m.Name,
                    imdb_id = m.GetProviderId(MetadataProviders.Imdb),
                    year = m.ProductionYear ?? 0
                };

                moviesPayload.Add(movieData);
            }

            var data = new
                           {
                               username = traktUser.UserName,
                               password = traktUser.PasswordHash,
                               movies = moviesPayload
                           };

            var dataString = _jsonSerializer.SerializeToString(data);

            Stream response = null;

            switch (eventType)
            {
                case EventType.Add:
                    response = await _httpClient.Post(TraktUris.MovieLibrary, dataString, Plugin.Instance.TraktResourcePool,
                                                      cancellationToken).ConfigureAwait(false);
                    break;
                case EventType.Remove:
                    response = await _httpClient.Post(TraktUris.MovieUnLibrary, dataString, Plugin.Instance.TraktResourcePool,
                                                      cancellationToken).ConfigureAwait(false);
                    break;
            }

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }


        /// <summary>
        /// Add or remove a list of Episodes to/from the users trakt.tv library
        /// </summary>
        /// <param name="episodes">The episodes to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{TraktResponseDataContract}.</returns>
        public async Task<TraktResponseDataContract> SendLibraryUpdateAsync(IReadOnlyList<Episode> episodes, TraktUser traktUser, CancellationToken cancellationToken, EventType eventType)
        {
            if (episodes == null)
                throw new ArgumentNullException("episodes");

            if (traktUser == null)
                throw new ArgumentNullException("traktUser");

            if (eventType == EventType.Update) return null;

            var episodesPayload = new List<object>();

            foreach (var ep in episodes)
            {
                var episodeData = new
                {
                    season = ep.ParentIndexNumber,
                    episode = ep.IndexNumber
                };

                episodesPayload.Add(episodeData);
            }

            var data = new
                           {
                               username = traktUser.UserName,
                               password = traktUser.PasswordHash,
                               imdb_id = episodes[0].Series.GetProviderId(MetadataProviders.Imdb),
                               tvdb_id = episodes[0].Series.GetProviderId(MetadataProviders.Tvdb),
                               title = episodes[0].Series.Name,
                               year = (episodes[0].Series.ProductionYear ?? 0).ToString(CultureInfo.InvariantCulture),
                               episodes = episodesPayload
                           };
            
            var dataString = _jsonSerializer.SerializeToString(data);

            Stream response = null;

            switch (eventType)
            {
                case EventType.Add:
                    response = await _httpClient.Post(TraktUris.ShowEpisodeLibrary, dataString, Plugin.Instance.TraktResourcePool,
                                                      cancellationToken).ConfigureAwait(false);
                    break;
                case EventType.Remove:
                    response = await _httpClient.Post(TraktUris.ShowEpisodeUnLibrary, dataString, Plugin.Instance.TraktResourcePool,
                                                      cancellationToken).ConfigureAwait(false);
                    break;
            }

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// Rate an item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rating"></param>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<TraktResponseDataContract> SendItemRating(BaseItem item, int rating, TraktUser traktUser)
        {
            var url = "";
            var data = new Dictionary<string, string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            
            if (item is Movie)
            {
                try
                {
                    data.Add("imdb_id", item.ProviderIds["Imdb"]);
                }
                catch (Exception)
                { }
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                url = TraktUris.RateMovie;
            }
            else if (item is Episode)
            {
                data.Add("title", ((Episode)item).Series.Name);
                data.Add("year", ((Episode)item).Series.ProductionYear != null ? ((Episode)item).Series.ProductionYear.ToString() : "");
                try
                {
                    data.Add("imdb_id", ((Episode)item).Series.ProviderIds["Imdb"]);
                }
                catch (Exception)
                { }
                try
                {
                    data.Add("tvdb_id", ((Episode)item).Series.ProviderIds["Tvdb"]);
                }
                catch (Exception)
                {}
                
                data.Add("season", ((Episode)item).Season.IndexNumber.ToString());
                data.Add("episode", item.IndexNumber.ToString());
                url = TraktUris.RateEpisode;
            }
            else // It's a Series
            {
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                try
                {
                    data.Add("imdb_id", item.ProviderIds["Imdb"]);
                }
                catch (Exception)
                { }
                try
                {
                    data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
                }
                catch (Exception)
                {}
                url = TraktUris.RateShow;
            }

            data.Add("rating", rating.ToString(CultureInfo.InvariantCulture));

            Stream response =
                await
                _httpClient.Post(url, data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="comment"></param>
        /// <param name="containsSpoilers"></param>
        /// <param name="traktUser"></param>
        /// <param name="isReview"></param>
        /// <returns></returns>
        public async Task<TraktResponseDataContract> SendItemComment(BaseItem item, string comment, bool containsSpoilers, TraktUser traktUser, bool isReview = false)
        {
            var url = "";
            var data = new Dictionary<string, string>();

            data.Add("username", traktUser.UserName);
            data.Add("password", traktUser.PasswordHash);
            
            if (item is Movie)
            {
                try
                {
                    data.Add("imdb_id", item.ProviderIds["Imdb"]);
                }
                catch (Exception)
                { }
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                url = TraktUris.CommentMovie;
            }
            else if (item is Episode)
            {
                try
                {
                    data.Add("imdb_id", ((Episode)item).Series.ProviderIds["Imdb"]);
                }
                catch (Exception)
                { }
                data.Add("title", ((Episode)item).Series.Name);
                data.Add("year", ((Episode)item).Series.ProductionYear != null ? ((Episode)item).Series.ProductionYear.ToString() : "");
                try
                {
                    data.Add("tvdb_id", ((Episode)item).Series.ProviderIds["Tvdb"]);
                }
                catch (Exception)
                {}
                
                data.Add("season", ((Episode)item).Season.IndexNumber.ToString());
                data.Add("episode", item.IndexNumber.ToString());
                url = TraktUris.CommentEpisode;   
            }
            else // It's a Series
            {
                try
                {
                    data.Add("imdb_id", item.ProviderIds["Imdb"]);
                }
                catch (Exception)
                { }
                data.Add("title", item.Name);
                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
                try
                {
                    data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
                }
                catch (Exception)
                {}
                
                url = TraktUris.CommentShow;
            }

            data.Add("comment", comment);
            data.Add("spoiler", containsSpoilers.ToString());
            data.Add("review", isReview.ToString());

            Stream response =
                await
                _httpClient.Post(url, data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<List<TraktMovieDataContract>> SendMovieRecommendationsRequest(TraktUser traktUser)
        {
            var data = new Dictionary<string, string>
                           {{"username", traktUser.UserName}, {"password", traktUser.PasswordHash}};

            Stream response =
                await
                _httpClient.Post(TraktUris.RecommendationsMovies, data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<List<TraktMovieDataContract>>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<List<TraktShowDataContract>> SendShowRecommendationsRequest(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            Stream response =
                await
                _httpClient.Post(TraktUris.RecommendationsShows, data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<List<TraktShowDataContract>>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<List<TraktMovieDataContract>>  SendGetAllMoviesRequest(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            var response =
                await
                _httpClient.Post(string.Format(TraktUris.MoviesAll, traktUser.UserName), data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<List<TraktMovieDataContract>>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<List<TraktUserLibraryShowDataContract>> SendGetCollectionShowsRequest(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            var response =
                await
                _httpClient.Post(string.Format(TraktUris.ShowsCollection, traktUser.UserName), data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<List<TraktUserLibraryShowDataContract>>(response);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="traktUser"></param>
        /// <returns></returns>
        public async Task<List<TraktUserLibraryShowDataContract>> SendGetWatchedShowsRequest(TraktUser traktUser)
        {
            var data = new Dictionary<string, string> { { "username", traktUser.UserName }, { "password", traktUser.PasswordHash } };

            var response =
                await
                _httpClient.Post(string.Format(TraktUris.ShowsWatched, traktUser.UserName), data, Plugin.Instance.TraktResourcePool,
                                                 CancellationToken.None).ConfigureAwait(false);

            return _jsonSerializer.DeserializeFromStream<List<TraktUserLibraryShowDataContract>>(response);
        }
    }
}
