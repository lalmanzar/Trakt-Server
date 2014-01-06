﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
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
        private readonly ISessionManager _sessionManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private TraktApi _traktApi;
        private TraktUriService _service;
        private LibraryManagerEventsHelper _libraryManagerEventsHelper;
        private List<ProgressEvent> _progressEvents;
        private UserDataManagerEventsHelper _userDataManagerEventsHelper;

        public static ServerMediator Instance { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jsonSerializer"></param>
        /// <param name="userManager"></param>
        /// <param name="sessionManager"> </param>
        /// <param name="userDataManager"></param>
        /// <param name="libraryManager"> </param>
        /// <param name="logger"></param>
        public ServerMediator(IJsonSerializer jsonSerializer, IUserManager userManager, ISessionManager sessionManager, IUserDataManager userDataManager, ILibraryManager libraryManager, ILogManager logger, IHttpClient httpClient)
        {
            Instance = this;
            _jsonSerializer = jsonSerializer;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _logger = logger.GetLogger("Trakt");

            _traktApi = new TraktApi(_jsonSerializer, _logger, httpClient);
            _service = new TraktUriService(_traktApi, _userManager, _logger);
            _libraryManagerEventsHelper = new LibraryManagerEventsHelper(_logger, _traktApi);
            _progressEvents = new List<ProgressEvent>();
            _userDataManagerEventsHelper = new UserDataManagerEventsHelper(_logger, _traktApi);

            // This should probably be elsewhere.
            UpdateUserRatingFormat();

            EnableUserDataSavedEventListener();
        }



        public void EnableUserDataSavedEventListener()
        {
            _userDataManager.UserDataSaved -= _userDataManager_UserDataSaved;
            _userDataManager.UserDataSaved += _userDataManager_UserDataSaved;
        }



        public void DisableUserDataSavedEventListener()
        {
            _userDataManager.UserDataSaved -= _userDataManager_UserDataSaved;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _userDataManager_UserDataSaved(object sender, UserDataSaveEventArgs e)
        {
            // ignore change events for any reason other than manually toggling played.
            if (e.SaveReason != UserDataSaveReason.TogglePlayed) return;

            var baseItem = e.Item as BaseItem;

            if (baseItem != null)
            {
                // determine if user has trakt credentials
                var traktUser = UserHelper.GetTraktUser(e.UserId.ToString());

                // Can't progress
                if (traktUser == null || baseItem.Path == null || baseItem.LocationType == LocationType.Virtual || (!(baseItem is Movie) && !(baseItem is Episode)))
                    return;

                foreach (var s in traktUser.TraktLocations.Where(s => baseItem.Path.StartsWith(s + Path.DirectorySeparatorChar)))
                {
                    // We have a user and the item is in a trakt monitored location. 
                    _userDataManagerEventsHelper.ProcessUserDataSaveEventArgs(e, traktUser);
                }
            }
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
            if (e.Item.LocationType == LocationType.Virtual) return;

            _logger.Info(e.Item.Name + "' reports removed from local library");
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
            if (e.Item.LocationType == LocationType.Virtual) return;

            _logger.Info(e.Item.Name + "' reports added to local library");
            _libraryManagerEventsHelper.QueueItem(e.Item, EventType.Add);
        }



        /// <summary>
        /// Let Trakt.tv know the user has started to watch something
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            try
            {
                _logger.Info("Playback Started");

                // Since MB3 is user profile friendly, I'm going to need to do a user lookup every time something starts
                var traktUser = UserHelper.GetTraktUser(e.Users.First());

                if (traktUser == null)
                {
                    _logger.Info("Could not match user " + e.Users.First().Id + " with any stored credentials");
                    return;
                }
                // Still need to make sure it's a trakt monitored location before sending notice to trakt.tv
                if (traktUser.TraktLocations == null)
                {
                    _logger.Info("User does not have any locations configured to monitor");
                    return;
                }

                var locations = traktUser.TraktLocations.Where(location => e.Item.Path.ToLower().Contains(location.ToLower() + "\\")).Where(
                    location => e.Item is Episode || e.Item is Movie).ToList();

                if (locations.Any())
                {
                    _logger.Debug(traktUser.LinkedMbUserId + " appears to be monitoring " + e.Item.Path);

                    foreach (var video in locations.Select(location => e.Item as Video))
                    {
                        try
                        {
                            if (video is Movie)
                            {
                                _logger.Debug("Send movie status update");
                                await
                                    _traktApi.SendMovieStatusUpdateAsync(video as Movie, MediaStatus.Watching, traktUser).
                                              ConfigureAwait(false);
                            }
                            else if (video is Episode)
                            {
                                _logger.Debug("Send episode status update");
                                await
                                    _traktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Watching, traktUser).
                                              ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.ErrorException("Exception handled sending status update", ex);
                        }
                        

                        var playEvent = new ProgressEvent
                                            {
                                                UserId = e.Users.First().Id,
                                                ItemId = e.Item.Id,
                                                LastApiAccess = DateTime.UtcNow
                                            };

                        _progressEvents.Add(playEvent);
                    }
                }
                else
                {
                    _logger.Debug(traktUser.LinkedMbUserId + " does not appear to be monitoring " + e.Item.Path);
                }

                
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error sending watching status update", ex, null);
            }
        }



        /// <summary>
        /// Let trakt.tv know that the user is still actively watching the media.
        /// 
        /// Event fires based on the interval that the connected client reports playback progress 
        /// to the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void KernelPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            _logger.Debug("Playback Progress");

            var playEvent =
                _progressEvents.FirstOrDefault(ev => ev.UserId.Equals(e.Users.First().Id) && ev.ItemId.Equals(e.Item.Id));

            if (playEvent == null) return;

            // Only report progress to trakt every 5 minutes
            if ((DateTime.UtcNow - playEvent.LastApiAccess).TotalMinutes >= 5)
            {
                var video = e.Item as Video;

                var traktUser = UserHelper.GetTraktUser(e.Users.First());

                if (traktUser == null) return;
                
                try
                {
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
                catch (Exception ex)
                {
                    _logger.ErrorException("Exception handled sending status update", ex);
                }
                // Reset the value
                playEvent.LastApiAccess = DateTime.UtcNow;
            }

        }



        /// <summary>
        /// Media playback has stopped. Depending on playback progress, let Trakt.tv know the user has
        /// completed watching the item.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void KernelPlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            try
            {
                var traktUser = UserHelper.GetTraktUser(e.Users.First());

                if (traktUser == null) return;

                // Still need to make sure it's a trakt monitored location before sending notice to trakt.tv
                if (traktUser.TraktLocations == null) return;

                foreach (
                    var location in 
                        traktUser.TraktLocations.Where(location => e.Item.Path.ToLower().Contains(location.ToLower() + "\\")).Where(
                            location => e.Item is Episode || e.Item is Movie))
                {
                    var video = e.Item as Video;

                    if (e.PlayedToCompletion)
                    {
                        _logger.Info("Item is played. Scrobble");

                        try
                        {
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
                        catch (Exception ex)
                        {
                            _logger.ErrorException("Exception handled sending status update", ex);
                        }

                    }
                    else
                    {
                        _logger.Info("Item Not fully played. Tell trakt.tv we are no longer watching but don't scrobble");

                        if (video is Movie)
                        {
                            await _traktApi.SendCancelWatchingMovie(traktUser);
                        }
                        else
                        {
                            await _traktApi.SendCancelWatchingShow(traktUser);
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error sending scrobble", ex, null);
            }

            // No longer need to track the item
            var playEvent =
                _progressEvents.FirstOrDefault(ev => ev.UserId.Equals(e.Users.First().Id) && ev.ItemId.Equals(e.Item.Id));

            if (playEvent != null)
                _progressEvents.Remove(playEvent);
        }



        /// <summary>
        /// Update each users rating format, simple/advanced, from trakt.tv
        /// </summary>
        /// <returns></returns>
        private async Task UpdateUserRatingFormat()
        {
            if (Plugin.Instance.Configuration.TraktUsers == null) return;

            foreach (var tUser in Plugin.Instance.Configuration.TraktUsers)
            {
                var account = await _traktApi.GetUserAccount(tUser);

                tUser.UsesAdvancedRating = account.Viewing.Ratings.Mode.ToLower() == "advanced";
            }

            Plugin.Instance.SaveConfiguration();
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
            _progressEvents = null;

        }
    }



    /// <summary>
    /// 
    /// </summary>
    public class ProgressEvent
    {
        public Guid UserId;
        public Guid ItemId;
        public DateTime LastApiAccess;
    }
}