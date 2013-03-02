using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using System;
using System.ComponentModel.Composition;
using Trakt.Configuration;

namespace Trakt
{
    [Export(typeof(IPlugin))]
    public class Plugin : BasePlugin<PluginConfiguration>, IUIPlugin
    {
        private ServerMediator _mediator;
        private ClientMediator _clientMediator;

        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IUserManager _userManager;

        public Plugin(IHttpClient httpClient, IJsonSerializer jsonSerializer, IUserManager userManager)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _userManager = userManager;
            Instance = this;
        }

        protected override void InitializeOnServer(bool isFirstRun)
        {
            base.InitializeOnServer(isFirstRun);
            _mediator = new ServerMediator(_httpClient, _jsonSerializer, _userManager);
        }



        protected override void DisposeOnServer(bool dispose)
        {
            base.DisposeOnServer(dispose);
            _mediator.Dispose();
        }



        protected override void InitializeInUi()
        {
            base.InitializeInUi();
            _clientMediator = new ClientMediator();
        }



        protected override void DisposeInUI(bool dispose)
        {
            base.DisposeInUI(dispose);
            _clientMediator.Dispose();
        }



        public override string Name
        {
            get { return "Trakt"; }
        }



        public override string Description
        {
            get
            {
                return "Watch, rate and discover media using Trakt. The htpc just got more social";
            }
        }



        public static Plugin Instance { get; private set; }


        
        public PluginConfiguration PluginConfiguration
        {
            get { return Configuration; }
        }



        public Version MinimumRequiredUIVersion
        {
            get { return new Version(0, 0, 0, 1); }
        }
    }
}
