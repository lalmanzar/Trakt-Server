using MediaBrowser.Common.Plugins;
using System.ComponentModel.Composition;
using System.IO;
using MediaBrowser.Controller.Plugins;

namespace Trakt.Configuration
{
    [Export(typeof(BaseConfigurationPage))]
    class TraktConfigurationPage : BaseConfigurationPage
    {
        public override string Name
        {
            get { return "Trakt for MediaBrowser"; }
        }



        public override Stream GetHtmlStream()
        {
            return GetHtmlStreamFromManifestResource("Trakt.Configuration.configPage.html");
        }



        public override IPlugin GetOwnerPlugin()
        {
            return Plugin.Instance;
        }



        public override ConfigurationPageType ConfigurationPageType
        {
            get { return ConfigurationPageType.PluginConfiguration; }
        }
    }
}
