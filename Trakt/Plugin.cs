using System;
using System.ComponentModel.Composition;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using Trakt.Configuration;

namespace Trakt
{
    [Export(typeof(IPlugin))]
    public class Plugin : BaseUiPlugin<PluginConfiguration>
    {
        private ServerMediator _mediator;
        private ClientMediator _clientMediator;

        private readonly IJsonSerializer _jsonSerializer;

        public Plugin(IJsonSerializer jsonSerializer)
        {
            _jsonSerializer = jsonSerializer;
            Instance = this;
        }

        protected override void InitializeOnServer(bool isFirstRun)
        {
            base.InitializeOnServer(isFirstRun);
            _mediator = new ServerMediator(_jsonSerializer);
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



        public override Version MinimumRequiredUIVersion
        {
            get { return new Version(0, 0, 0, 1); }
        }
    }
}
