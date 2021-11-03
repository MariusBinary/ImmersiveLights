using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Core;
using ImmersiveLights.Pages;
using ImmersiveLights.Controls;
using Un4seen.Bass;

namespace ImmersiveLights.Frames
{
    public partial class AudioCaptureFrame : UserControl, IFrameMetadata
    {
        #region IFrameCallback
        public string Title { get { return FindResource("navbarAudioPreferences") as string; } }
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
            canChangeInputDevice = (status != ConnectionStatus.CONNECTED || effect != Effects.MUSIC);
        }
        #endregion

        #region Variables
        private string selectedDevice;
        private bool isInputAllowed;
        private bool canChangeInputDevice;
        private int oldInputDevice = -1;
        private bool restoreOldDevice = false;
        #endregion

        #region Main
        public AudioCaptureFrame()
        {
            InitializeComponent();

            var status = ((MainWindow)Application.Current.MainWindow).serialHandler.connectionStatus;
            var effect = ((MainWindow)Application.Current.MainWindow).currentEffect;
            canChangeInputDevice = (status != ConnectionStatus.CONNECTED || effect != Effects.MUSIC);

            LoadPreferences();
            LoadDevices();

            isInputAllowed = true;
        }

        /// <summary>
        /// Carica le preferenze dell'utente.
        /// </summary>
        public void LoadPreferences()
        {
            selectedDevice = Preferences.GetPreference<string>("music", "audioInputDevice");
            CBox_SamplingRate.SelectedIndex = Preferences.GetPreference<int>("music", "audioSamplingRate");
            Seek_VolumeSensibility.Value = Preferences.GetPreference<int>("music", "audioVolumeSensibility");
        }

        /// <summary>
        /// Carica tutti i dispositivi audio di input.
        /// </summary>
        public void LoadDevices()
        {
            CBox_InputDevice.Items.Add(FindResource("audioPreferencesDefaultName") as string);

            // Carica i dispositivi audio di output disponibili.
            BASS_DEVICEINFO info = new BASS_DEVICEINFO();
            for (int n = 1; Bass.BASS_GetDeviceInfo(n, info); n++)
            {
                CBox_InputDevice.Items.Add(info.ToString());
                if (selectedDevice.Equals(info.id)) {
                    CBox_InputDevice.SelectedIndex = n;
                }
            }

            if (selectedDevice.Equals("default")) {
                CBox_InputDevice.SelectedIndex = 0;
            }
        }
        #endregion

        #region Events
        private void CBox_SamplingRate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInputAllowed) return;
            var samplingRateIndex = CBox_SamplingRate.SelectedIndex;
            Preferences.SetPreference("music", "audioSamplingRate", samplingRateIndex);
            Core.Utils.NotifyEffect(Effects.MUSIC, "audioSamplingRate", samplingRateIndex);
        }

        private void CBox_InputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInputAllowed || restoreOldDevice)  {
                oldInputDevice = CBox_InputDevice.SelectedIndex;
                restoreOldDevice = false;
                return;
            }

            if (!canChangeInputDevice) {
                WpfMessageBox.Show(FindResource("alertChangeAudioDenialTitle") as string,
                    FindResource("alertChangeAudioDenialDescription") as string);

                restoreOldDevice = true;
                CBox_InputDevice.SelectedIndex = oldInputDevice;
                return;
            }

            if (CBox_InputDevice.SelectedIndex == 0) {
                Preferences.SetPreference("music", "audioInputDevice", "default");
            }  else {
                BASS_DEVICEINFO info = new BASS_DEVICEINFO();
                Bass.BASS_GetDeviceInfo(CBox_InputDevice.SelectedIndex, info);
                Preferences.SetPreference("music", "audioInputDevice", info.id);
            }
        }

        private void Seek_VolumeSensibility_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!isInputAllowed) return;
            var audioVolumeSensibility = Seek_VolumeSensibility.Value;
            Preferences.SetPreference("music", "audioVolumeSensibility", audioVolumeSensibility);
            Core.Utils.NotifyEffect(Effects.MUSIC, "audioVolumeSensibility", audioVolumeSensibility);
        }
        #endregion
    }
}
