using System.Windows;
using System.Windows.Controls;
using ImmersiveLights.Controls;
using ImmersiveLights.Core;
using ImmersiveLights.Interfaces;

namespace ImmersiveLights.Pages
{
    public partial class ScreenCaptureFrame : UserControl, IFrameMetadata
    {
        #region IFrameCallback
        public string Title { get { return FindResource("navbarCapturePreferences") as string; } }
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
            var effect = ((MainWindow)Application.Current.MainWindow).currentEffect;
            canChangeCaptureDevice = (status != ConnectionStatus.CONNECTED || effect != Effects.AMBILIGHT);
        }
        #endregion

        #region Variables
        private bool isInputAllowed;
        private bool canChangeCaptureDevice;
        private int oldInputDevice = -1;
        private bool restoreOldDevice = false;
        #endregion

        public ScreenCaptureFrame()
        {
            InitializeComponent();

            var status = ((MainWindow)Application.Current.MainWindow).serialHandler.connectionStatus;
            var effect = ((MainWindow)Application.Current.MainWindow).currentEffect;
            canChangeCaptureDevice = (status != ConnectionStatus.CONNECTED || effect != Effects.AMBILIGHT);


            LoadDisplays();
            LoadPreferences();

            isInputAllowed = true;
        }

        private void LoadPreferences()
        {
            CBox_CaptureScreen.SelectedIndex = Preferences.GetPreference<int>("ambilight", "captureDevice");
            CBox_CaptureFramerate.SelectedIndex = Preferences.GetPreference<int>("ambilight", "captureFramerate");
            Seek_LedTransition.Value = Preferences.GetPreference<double>("ambilight", "ledTransition");
            Seek_MinBrightness.Value = Preferences.GetPreference<double>("ambilight", "minBrightness");
        }

        private void LoadDisplays()
        {
            // Carica tutti i display disponibili.
            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            int primaryScreen = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                System.Windows.Forms.Screen screen = screens[i];
                CBox_CaptureScreen.Items.Add($"{screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})");
                primaryScreen = screen.Primary ? i : primaryScreen;
            }

            // Imposta il display principale.
            CBox_CaptureScreen.SelectedIndex = primaryScreen;
        }

        private void Seek_LedTransition_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!isInputAllowed) return;
            var ledTransition = Seek_LedTransition.Value;
            Preferences.SetPreference("ambilight", "ledTransition", ledTransition);
            Core.Utils.NotifyEffect(Effects.MUSIC, "ledTransition", ledTransition);
        }

        private void Seek_MinBrightness_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!isInputAllowed) return;
            var minBrightness = Seek_MinBrightness.Value;
            Preferences.SetPreference("ambilight", "minBrightness", minBrightness);
            Core.Utils.NotifyEffect(Effects.AMBILIGHT, "minBrightness", minBrightness);
        }

        private void CBox_CaptureFramerate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInputAllowed) return;
            var captureFramerateIndex = CBox_CaptureFramerate.SelectedIndex;
            Preferences.SetPreference("ambilight", "captureFramerate", captureFramerateIndex);
            Core.Utils.NotifyEffect(Effects.AMBILIGHT, "captureFramerate", captureFramerateIndex);
        }

        private void CBox_CaptureScreen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInputAllowed || restoreOldDevice)
            {
                oldInputDevice = CBox_CaptureScreen.SelectedIndex;
                restoreOldDevice = false;
                return;
            }

            if (!canChangeCaptureDevice)
            {
                WpfMessageBox.Show(FindResource("alertChangeDisplayDenialTitle") as string,
                    FindResource("alertChangeDisplayDenialDescription") as string);

                restoreOldDevice = true;
                CBox_CaptureScreen.SelectedIndex = oldInputDevice;
                return;
            }

            Preferences.SetPreference("ambilight", "captureDevice", CBox_CaptureScreen.SelectedIndex);
        }
    }
}
