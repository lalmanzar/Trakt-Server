using System;
using System.ComponentModel.Composition;

using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using Trakt.Configuration;

namespace Trakt
{
    [Export(typeof(IPlugin))]
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        private ServerMediator _mediator;

        protected override void InitializeOnServer(bool isFirstRun)
        {
            base.InitializeOnServer(isFirstRun);
            _mediator = new ServerMediator();
        }



        protected override void DisposeOnServer(bool dispose)
        {
            base.DisposeOnServer(dispose);
            _mediator.Dispose();
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

        public Plugin()
        {
            Instance = this;
        }


        
        public PluginConfiguration PluginConfiguration
        {
            get { return Configuration; }
        }
    }
}
