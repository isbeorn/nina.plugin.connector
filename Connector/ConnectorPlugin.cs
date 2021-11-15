using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugins.Connector
{
    [Export(typeof(IPluginManifest))]
    public class ConnectorPlugin : PluginBase {
        public ConnectorPlugin() {
            if (Properties.Settings.Default.UpdateSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }
        }
    }
}
