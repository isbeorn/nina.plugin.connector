using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugins.Connector
{
    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary
    {
        public Options()
        {
            InitializeComponent();
        }
    }
}
