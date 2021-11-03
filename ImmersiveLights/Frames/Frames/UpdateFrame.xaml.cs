using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net.Http;
using System.Diagnostics;
using System.Collections.Generic;
using ImmersiveLights.Core;
using ImmersiveLights.Interfaces;
using Newtonsoft.Json.Linq;

namespace ImmersiveLights.Pages
{
    public partial class UpdateFrame : UserControl, IFrameMetadata
    {
        #region IFrameCallback
        public string Title { get { return FindResource("navbarCheckForUpdates") as string; } }
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

        #region Variables
        private static readonly HttpClient client = new HttpClient();
        private bool isDownloadAvailable; 
        #endregion

        public UpdateFrame()
        {
            InitializeComponent();

            CheckUpdates();
        }

        private async void CheckUpdates()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            Tx_Version.Text = $"{version.Major}.{version.Minor}";

            var values = new Dictionary<string, string>
            {
                { "guid", "3f88b2b0-fefe-4e35-b511-7d1acf26e2a7" },
                { "version", $"{version.Major}.{version.Minor}"}
            };

            ChangeUpdateStatus(1);

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("http://mariusbinary.altervista.org/backend/immersive_lights/checkUpdates.php", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK) {
                JObject decodedResponse = JObject.Parse(responseString);
                isDownloadAvailable = (string)decodedResponse["message"] == "Update available";
                ChangeUpdateStatus(isDownloadAvailable ? 2 : 0);
            } else {
                ChangeUpdateStatus(0);
            }
        }

        private void Btn_CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!isDownloadAvailable) {
                CheckUpdates();
            } else {
                Process.Start("http://mariusbinary.altervista.org/documentation.php?id=immersive_lights");
            }
        }

        private void ChangeUpdateStatus(int status)
        {
            switch(status)
            {
                case 0:
                    Tx_State.Text = FindResource("updatesStatusUpdated") as string;
                    Tx_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 124, 204, 6));
                    (Btn_CheckForUpdates.Content as TextBlock).Text = FindResource("updatesCheckForUpdatesButton") as string;
                    break;
                case 1:
                    Tx_State.Text = FindResource("updatesStatusChecking") as string;
                    Tx_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 163, 161, 162));
                    (Btn_CheckForUpdates.Content as TextBlock).Text = FindResource("updatesCheckForUpdatesButton") as string;
                    break;
                case 2:
                    Tx_State.Text = FindResource("updatesStatusOutdated") as string;
                    Tx_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 211, 135, 63));
                    (Btn_CheckForUpdates.Content as TextBlock).Text = FindResource("updatesDownloadButton") as string;
                    break;
            }
        }
    }
}
