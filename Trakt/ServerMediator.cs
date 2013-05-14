using System;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System.Linq;
using Trakt.Api;
using Trakt.Helpers;

namespace Trakt
{
    /// <summary>
    /// All communication between the server and the plugins server instance should occur in this class.
    /// </summary>
    public class ServerMediator : IServerEntryPoint
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly ISessionManager _sessionManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataRepository _userDataRepository;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private TraktApi _traktApi;
        private TraktUriService _service;
        private LibraryManagerEventsHelper _libraryManagerEventsHelper;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="jsonSerializer"></param>
        /// <param name="userManager"></param>
        /// <param name="sessionManager"> </param>
        /// <param name="userDataRepository"></param>
        /// <param name="libraryManager"> </param>
        /// <param name="logger"></param>
        public ServerMediator(IHttpClient httpClient, IJsonSerializer jsonSerializer, IUserManager userManager, ISessionManager sessionManager, IUserDataRepository userDataRepository, ILibraryManager libraryManager, ILogger logger)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _userDataRepository = userDataRepository;
            _libraryManager = libraryManager;
            _logger = logger;
            _traktApi = new TraktApi(_httpClient, _jsonSerializer, _logger);
            _service = new TraktUriService(_traktApi, _userManager);
            _libraryManagerEventsHelper = new LibraryManagerEventsHelper(_logger, _traktApi);
        }



        /// <summary>
        /// 
        /// </summary>
        public void Run()
        {
            _sessionManager.PlaybackStart += KernelPlaybackStart;
            _sessionManager.PlaybackProgress += KernelPlaybackProgress;
            _sessionManager.PlaybackStopped += KernelPlaybackStopped;
            _libraryManager.ItemAdded += LibraryManagerItemAdded;
            _libraryManager.ItemRemoved += LibraryManagerItemRemoved;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LibraryManagerItemRemoved(object sender, ItemChangeEventArgs e)
        {
            if (!(e.Item is Movie) && !(e.Item is Episode) && !(e.Item is Series)) return;

            _logger.Info("Trakt: '" + e.Item.Name + "' reports removed from local library");
            _libraryManagerEventsHelper.QueueItem(e.Item, EventType.Remove);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LibraryManagerItemAdded(object sender, ItemChangeEventArgs e)
        {
            // Don't do anything if it's not a supported media type
            if (!(e.Item is Movie) && !(e.Item is Episode) && !(e.Item is Series)) return;

            _logger.Info("Trakt: '" + e.Item.Name + "' reports added to local library");
            _libraryManagerEventsHelper.QueueItem(e.Item, EventType.Add);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            try
            {
                _logger.Info("TRAKT: Playback Started");
                // Since MB3 is user profile friendly, I'm going to need to do a user lookup every time something starts
                var traktUser = UserHelper.GetTraktUser(e.User);

                if (traktUser == null) return;
                // Still need to make sure it's a trakt monitored location before sending notice to trakt.tv
                if (traktUser.TraktLocations == null) return;

                foreach (
                    var location in
                        traktUser.TraktLocations.Where(location => e.Item.Path.Contains(location + "\\")).Where(
                            location => e.Item is Episode || e.Item is Movie))
                {
                    var video = e.Item as Video;

                
                    if (video is Movie)
                    {

                        await
                            _traktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Watching, traktUser).
                                ConfigureAwait(false);
                    }
                    else if (video is Episode)
                    {
                        await
                            _traktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Watching, traktUser).
                                ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Trakt: Error sending watching status update", ex, null);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KernelPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void KernelPlaybackStopped(object sender, PlaybackProgressEventArgs e)
        {
            if (e.PlaybackPositionTicks == null) return;

            try
            {
                var userData = await _userDataRepository.GetUserData(e.User.Id, e.Item.GetUserDataKey()).ConfigureAwait(false);

                if (userData.Played)
                {
                    var traktUser = UserHelper.GetTraktUser(e.User);

                    if (traktUser == null) return;

                    // Still need to make sure it's a trakt monitored location before sending notice to trakt.tv
                    if (traktUser.TraktLocations == null) return;

                    foreach (
                        var location in 
                            traktUser.TraktLocations.Where(location => e.Item.Path.Contains(location + "\\")).Where(
                                location => e.Item is Episode || e.Item is Movie))
                    {
                        var video = e.Item as Video;

                    
                        if (video is Movie)
                        {
                            await
                                _traktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Scrobble, traktUser).
                                    ConfigureAwait(false);
                        }
                        else if (video is Episode)
                        {
                            await
                                _traktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Scrobble, traktUser)
                                    .ConfigureAwait(false);
                        }
                    
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Trakt: Error sending scrobble", ex, null);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            _sessionManager.PlaybackStart -= KernelPlaybackStart;
            _sessionManager.PlaybackProgress -= KernelPlaybackProgress;
            _sessionManager.PlaybackStopped -= KernelPlaybackStopped;
            _libraryManager.ItemAdded -= LibraryManagerItemAdded;
            _libraryManager.ItemRemoved -= LibraryManagerItemRemoved;
            _service = null;
            _traktApi = null;
            _libraryManagerEventsHelper = null;
        }
    }
}