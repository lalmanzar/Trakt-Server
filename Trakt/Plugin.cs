using MediaBrowser.Common.Kernel;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.ComponentModel.Composition;
using Trakt.Configuration;

namespace Trakt
{
    [Export(typeof(IPlugin))]
    public class Plugin : BasePlugin<PluginConfiguration>, IUIPlugin
    {
        public Plugin(IKernel kernel, IXmlSerializer xmlSerializer) : base(kernel, xmlSerializer)
        {
            Instance = this;
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
