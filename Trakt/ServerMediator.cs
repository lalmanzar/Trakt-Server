using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Linq;

namespace Trakt
{
    /// <summary>
    /// All communication between the server and the plugins server instance should occur in this class.
    /// Once the hookable events are created in core, they should be hooked here.
    /// </summary>
    public class ServerMediator : IServerEntryPoint
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IUserManager _userManager;

        public ServerMediator(IHttpClient httpClient, IJsonSerializer jsonSerializer, IUserManager userManager)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _userManager = userManager;
        }

        public void Run()
        {
            _userManager.PlaybackStart += KernelPlaybackStart;
            _userManager.PlaybackProgress += KernelPlaybackProgress;
            _userManager.PlaybackStopped += KernelPlaybackStopped;
        }

        private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            // Since MB3 is user profile friendly, I'm going to need to do a user lookup every time something starts
            var traktUser = UserHelper.GetTraktUser(e.User);

            if (traktUser == null) return;
            // Still need to make sure it's a trakt monitored location before sending notice to trakt.tv
            if (traktUser.TraktLocations == null) return;

            foreach (
                var location in
                    traktUser.TraktLocations.Where(location => e.Argument.Path.Contains(location + "\\")).Where(
                        location => e.Argument is Episode || e.Argument is Movie))
            {
                await TraktGateway.SendWatchingState(e.Argument as Video, traktUser, _httpClient, _jsonSerializer).ConfigureAwait(false);
            }
        }


        private void KernelPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
        }

        private async void KernelPlaybackStopped(object sender, PlaybackProgressEventArgs e)
        {
            if (e.PlaybackPositionTicks == null) return;

            var userData = e.Argument.GetUserData(e.User, false);

            if (userData.Played)
            {
                var traktUser = UserHelper.GetTraktUser(e.User);

                if (traktUser == null) return;

                // Still need to make sure it's a trakt monitored location before sending notice to trakt.tv
                if (traktUser.TraktLocations == null) return;

                foreach (
                    var location in 
                        traktUser.TraktLocations.Where(location => e.Argument.Path.Contains(location + "\\")).Where(
                            location => e.Argument is Episode || e.Argument is Movie))
                {
                    await TraktGateway.SendScrobbleState(e.Argument as Video, traktUser, _httpClient, _jsonSerializer).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            _userManager.PlaybackStart -= KernelPlaybackStart;
            _userManager.PlaybackProgress -= KernelPlaybackProgress;
            _userManager.PlaybackStopped -= KernelPlaybackStopped;
        }
    }
}