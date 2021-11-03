using System.Windows.Controls;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Core;

namespace ImmersiveLights.Pages
{
    public partial class AboutFrame : UserControl, IFrameMetadata
    {
        #region IFrameCallback
        public string Title { get { return FindResource("navbarAbout") as string; } }
        public double InitialHeight { get { return 100.0; } }

        public bool TryToClose(string msg)
        {
            return true;
        }
        /// <summary>
        /// Indica un cambio dello stato di collegamento.
        /// </summary>
        public void OnConnectionChanged(ConnectionStatus status)
        {
        }
        #endregion

        public AboutFrame()
        {
            InitializeComponent();
        }
    }
}
