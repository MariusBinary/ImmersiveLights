using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImmersiveLights.Core;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Models;
using ImmersiveLights.Structures;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace ImmersiveLights.Frames
{
    public partial class MusicFrame : UserControl, IEffectCallback
    {
        #region Variables
        private List<int> inputIndexes = new List<int>();
        private List<int> inputFrequencies = new List<int>();
        private List<int> inputChannels = new List<int>();
        private int deviceIndex = -1;
        private WASAPIPROC process;
        private float[] fftBuffer = new float[8192];
        private int fftChannels = 60;
        private int fftFilter = 0;
        private int fftMultiplier = 1;
        private int[] minfilters = new int[] { 00, 19, 39 };
        private int[] maxfilters = new int[] { 19, 39, 59 };
        private byte[] serialData;
        private byte[][] gamma;
        private TimerHandler timer;
        private IFrameCallback serial;
        private int currentEffect = 0;
        private int totalLeds;
        private double maxBrightness = 255;
        private byte r, g, b;
        private bool isInputAllowed;
        #endregion

        #region Main
        public MusicFrame(IFrameCallback serial)
        {
            InitializeComponent();
            timer = new TimerHandler();
            LoadPreferences();

            this.serial = serial;
            timer.Create(InitializeBassDLL, OnUpdate);
            timer.Start();
        }

        private void LoadPreferences()
        {
            //Carica i dispositivi audio di output disponibili.
            int devicecount = BassWasapi.BASS_WASAPI_GetDeviceCount();
            for (int i = 0; i < devicecount; i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device.IsEnabled && device.IsLoopback)
                {
                    inputIndexes.Add(i);
                    inputFrequencies.Add(device.mixfreq);
                    inputChannels.Add(device.mixchans);
                }
            }

            // Carica le preferenze relative al moltiplicatore di picco.
            string selectedDevice = Preferences.GetPreference<string>("music", "audioInputDevice");

            BASS_DEVICEINFO info = new BASS_DEVICEINFO();
            for (int n = 1; Bass.BASS_GetDeviceInfo(n, info); n++)
            {
                if ((selectedDevice.Equals("default") && info.IsDefault) || 
                    selectedDevice.Equals(info.id)) {
                    deviceIndex = n - 1;
                }
            }

            bool lightSwitch = Preferences.GetPreference<bool>("settings", "lightSwitch");
            double lightIntensity = Preferences.GetPreference<double>("settings", "lightIntensity");
            maxBrightness = (short)(lightSwitch ? lightIntensity : 0.0);

            ColorModel selected = Preferences.GetPreference<ColorModel>("music", "selectedColor");
            ColorPicker.SelectedColor = Color.FromRgb(selected.Red, selected.Green, selected.Blue);

            // Carica le preferenze relative al moltiplicatore di picco.
            fftMultiplier = Preferences.GetPreference<int>("music", "audioVolumeSensibility");

            // Carica le preferenze relative al filtro della banda.
            fftFilter = Preferences.GetPreference<int>("music", "audioBandFilter");

            switch (fftFilter)
            {
                case 0:
                    Rad_FilterLow.IsChecked = true;
                    break;
                case 1:
                    Rad_FilterMedium.IsChecked = true;
                    break;
                case 2:
                    Rad_FilterHigh.IsChecked = true;
                    break;
            }

            // Carica le preferenze relative alla sensibilità del picco.
            fftMultiplier = Core.Preferences.GetPreference<int>("music", "audioVolumeSensibility");

            // Imposta l'intervallo di cattura.
            int captureIntensity = Preferences.GetPreference<int>("music", "audioSamplingRate");
            int[] captureIntervals = new int[] { 8, 32, 64 };
            timer.SetInterval(captureIntervals[captureIntensity]);

            var arrangement = Preferences.GetPreference<ArrangementConfig>("settings", "arrangement");

            totalLeds = arrangement.top.leds + arrangement.bottom.leds +
                arrangement.left.leds + arrangement.right.leds;

            // Inizializza gli array
            serialData = new byte[10 + totalLeds * 3];

            // A special header / magic word is expected by the corresponding LED
            // streaming code running on the Arduino.  This only needs to be initialized
            // once (not in draw() loop) because the number of LEDs remains constant:
            serialData[0] = (byte)0x49;
            serialData[1] = (byte)0x6D;
            serialData[2] = (byte)0x6D;
            serialData[3] = (byte)0x65;
            serialData[4] = (byte)0x72;
            serialData[5] = (byte)0x73;
            serialData[6] = (byte)0x69;
            serialData[7] = (byte)0x76;
            serialData[8] = (byte)0x65;
            serialData[9] = (byte)0x4C;

            var colorCorrection = Preferences.GetPreference<ColorCorrectionConfig>("settings", "colorCorrection");
            ColorCorrection.GenerateColorCorrectionMap(colorCorrection, out gamma);

            // Carica le preferenze relative alla sensibilità del picco.
            currentEffect = Preferences.GetPreference<int>("music", "currentEffect");
            CBox_SelectedEffect.SelectedIndex = currentEffect;

            isInputAllowed = true;
        }
        #endregion

        #region Logic
        public void InitializeBassDLL()
        {
            // Configura il processo di BassDLL.
            process = new WASAPIPROC(Process);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            Bass.BASS_Init(0, inputFrequencies[deviceIndex], BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

            // Inizializza il processo WASAPI di BassDLL.
            if (BassWasapi.BASS_WASAPI_Init(inputIndexes[deviceIndex], inputFrequencies[deviceIndex],
                inputChannels[deviceIndex], BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, process, IntPtr.Zero))
                BassWasapi.BASS_WASAPI_Start();
        }

        public void OnUpdate()
        {
            // Ottiene i dati dal dispositivo di output.
            int ret = BassWasapi.BASS_WASAPI_GetData(fftBuffer, (int)BASSData.BASS_DATA_FFT8192);
            if (ret < -1) return;

            // Analizza i dati e fa una media del livello delle frequenze che rientrano nella
            // banda selezionata dall'utente.
            int x = 0, y = 0, b0 = 0;
            double average = 0;
            for (x = minfilters[fftFilter]; x < maxfilters[fftFilter]; x++)
            {
                float peak = 0;
                int b1 = (int)Math.Pow(2, x * 10.0 / (fftChannels - 1));

                if (b1 > 1023) b1 = 1023;
                if (b1 <= b0) b1 = b0 + 1;

                for (; b0 < b1; b0++)
                    if (peak < fftBuffer[1 + b0]) 
                        peak = fftBuffer[1 + b0];

                y = (int)(Math.Sqrt(peak) * 3 * 255 - 4);
                y = y > 255 ? 255 : y;
                y = y < 0 ? 0 : y;

                average += y;
            }

            double scale = maxBrightness / 255.0;
            double fftMultiplierNormalized = Clamp((average * fftMultiplier) / 20.0, 0.0, 255.0);
            average = (fftMultiplierNormalized / 255.0) * scale;

            int i, j;
            j = 10;

            byte outR = r, outG = g, outB = b;
            switch(currentEffect)
            {
                case 1:
                    Color color = Rainbow(rainbowProgress);
                    outR = color.R;
                    outG = color.G;
                    outB = color.B;
                    rainbowProgress += 0.001f;
                    if (rainbowProgress > 1.0f) {
                        rainbowProgress = 0.0f;
                    }
                    break;
            }

            for (i = 0; i < totalLeds; i++)
            {
                // Apply gamma curve and place in serial output buffer
                serialData[j++] = gamma[(byte)(outR * average)][0];
                serialData[j++] = gamma[(byte)(outG * average)][1];
                serialData[j++] = gamma[(byte)(outB * average)][2];
            }

            serial.OnDataAvailable(serialData);
        }

        private T Clamp<T>(T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        private float rainbowProgress = 0.0f;

        public static Color Rainbow(float progress)
        {
            float div = (Math.Abs(progress % 1) * 6);
            byte ascending = (byte)((div % 1) * 255);
            byte descending = (byte)(255 - ascending);

            switch ((int)div)
            {
                case 0:
                    return Color.FromArgb(255, 255, ascending, 0);
                case 1:
                    return Color.FromArgb(255, descending, 255, 0);
                case 2:
                    return Color.FromArgb(255, 0, 255, ascending);
                case 3:
                    return Color.FromArgb(255, 0, descending, 255);
                case 4:
                    return Color.FromArgb(255, ascending, 0, 255);
                default:
                    return Color.FromArgb(255, 255, 0, descending);
            }
        }
        #endregion

        #region Events
        private void Rad_FilterLow_Click(object sender, RoutedEventArgs e)
        {
            if (Rad_FilterLow.IsChecked.Value)
            {
                fftFilter = 0;
                Core.Preferences.SetPreference<int>("music", "audioBandFilter", 0);
            }
        }
        private void Rad_FilterMedium_Click(object sender, RoutedEventArgs e)
        {
            if (Rad_FilterMedium.IsChecked.Value)
            {
                fftFilter = 1;
                Core.Preferences.SetPreference<int>("music", "audioBandFilter", 1);
            }
        }
        private void Rad_FilterHigh_Click(object sender, RoutedEventArgs e)
        {
            if (Rad_FilterHigh.IsChecked.Value)
            {
                fftFilter = 2;
                Core.Preferences.SetPreference<int>("music", "audioBandFilter", 2);
            }
        }
        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            var color = e.NewValue.Value;
            r = color.R;
            g = color.G;
            b = color.B;
        }
        #endregion

        #region IEffectCallback
        /// <summary>
        /// Ritorna il tipo di effetto.
        /// </summary>
        public Effects EffectType { get { return Effects.MUSIC; } }
        /// <summary>
        /// Indica che le impostazioni dell'effetto hanno subito una modifica
        /// e devono essere ricaricate.
        /// </summary>
        public void OnPreferencesChanged<T>(string key, T value)
        {
            switch (key)
            {
                case "audioSamplingRate":
                    var samplingRateIndex = Convert.ToInt32(value);
                    int[] captureIntervals = new int[] { 8, 32, 64 };
                    timer.SetInterval(captureIntervals[samplingRateIndex]);
                    break;
                case "audioVolumeSensibility":
                    fftMultiplier = Convert.ToInt32(value);
                    break;
            }
        }
        public void OnBrightnessChanged(double brightness)
        {
            this.maxBrightness = (short)brightness;
        }
        /// <summary>
        /// Indica che l'effetto deve iniziare.
        /// </summary>
        public void OnEffectStarted()
        {
            timer.Start();
        }

        private void CBox_SelectedEffect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInputAllowed) {
                currentEffect = CBox_SelectedEffect.SelectedIndex;
                Core.Preferences.SetPreference<int>("music", "currentEffect", currentEffect);
            }

            if (currentEffect == 0) {
                ColorPicker.IsEnabled = true;
                ColorPicker.Opacity = 1.0;
            } else {
                ColorPicker.IsEnabled = false;
                ColorPicker.Opacity = 0.4;
            }
        }

        /// <summary>
        /// Indica che l'effetto deve fermarsi.
        /// </summary>
        public void OnEffectStopped()
        {
            // Salva l'ultimo colore utilizzato.
            Preferences.SetPreference<ColorModel>("music", "selectedColor", new ColorModel() {
                Red = r,
                Green = g,
                Blue = b
            });

            timer.Stop(() => {
                // Arresta il processo di BassDLL.
                BassWasapi.BASS_WASAPI_Stop(true);
                BassWasapi.BASS_WASAPI_Free();
                Bass.BASS_Free();
            });
        }
        #endregion

        #region Utils
        private int Process(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }
        #endregion
    }
}
