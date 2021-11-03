using System.Windows;
using System.Windows.Controls;
using ImmersiveLights.Core;
using ImmersiveLights.Interfaces;

namespace ImmersiveLights.Pages
{
    public partial class SystemFrame : UserControl, IFrameMetadata
    {
        #region IFrameCallback
        public string Title { get { return FindResource("navbarSystemSettings") as string; } }
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

        public SystemFrame()
        {
            InitializeComponent();
            LoadPreferences();
        }

        /// <summary>
        /// Carica le preferenze dell'utente.
        /// </summary>
        public void LoadPreferences()
        {
            Sw_StartWithWindows.IsChecked = Preferences.GetPreference<bool>("settings", "startWithWindows");
            Sw_StartReducedToIcon.IsChecked = Preferences.GetPreference<bool>("settings", "startReducedToIcon");
            Sw_ReduceAsIconTry.IsChecked = Preferences.GetPreference<bool>("settings", "reduceAsIconTry");
        }

        private void Sw_StartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            Sw_StartWithWindows.IsChecked = Registry.ToggleStartup();
            Preferences.SetPreference("settings", "startWithWindows", Sw_StartWithWindows.IsChecked.Value);
        }

        private void Sw_StartReducedToIcon_Click(object sender, RoutedEventArgs e)
        {
            Preferences.SetPreference("settings", "startReducedToIcon", Sw_StartReducedToIcon.IsChecked.Value);
        }

        private void Sw_ReduceAsIconTry_Click(object sender, RoutedEventArgs e)
        {
            Preferences.SetPreference("settings", "reduceAsIconTry", Sw_ReduceAsIconTry.IsChecked.Value);
        }
    }
}
