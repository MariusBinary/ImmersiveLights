using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ImmersiveLights.Core;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Pages
{
    public partial class ColorCorrectionWizard : Window, INotifyPropertyChanged
    {
        #region Properties
        private bool _correctionEnabled = false;
        public bool CorrectionEnabled
        {
            get { return _correctionEnabled; }
            set
            {
                _correctionEnabled = value;
                RaisePropertyChanged("CorrectionEnabled");

            }
        }

        private double _gammaCorrection = 1.0;
        public double GammaCorrection
        {
            get { return _gammaCorrection; }
            set
            {
                _gammaCorrection = value;
                RaisePropertyChanged("GammaCorrection");

            }
        }

        private double _redCorrection = 255;
        public double RedCorrection
        {
            get { return _redCorrection; }
            set
            {
                _redCorrection = value;
                RaisePropertyChanged("RedCorrection");

            }
        }

        private double _greenCorrection = 255;
        public double GreenCorrection
        {
            get { return _greenCorrection; }
            set
            {
                _greenCorrection = value;
                RaisePropertyChanged("GreenCorrection");

            }
        }

        private double _blueCorrection = 255;
        public double BlueCorrection
        {
            get { return _blueCorrection; }
            set
            {
                _blueCorrection = value;
                RaisePropertyChanged("BlueCorrection");

            }
        }
        #endregion

        #region Variables
        private byte[][] gamma;
        private TimerHandler timer;
        private IFrameCallback serial;
        private double f;
        private double oldGamma, oldRed, oldGreen, oldBlue;
        private bool oldEnabled;
        private bool forceUpdate;
        private Color color;
        private byte[] serialData;
        private int totalLeds;
        #endregion

        #region Main
        public ColorCorrectionWizard(IFrameCallback serial)
        {
            InitializeComponent();

            this.serial = serial;
            timer = new TimerHandler();
            timer.Create(new Action(() => { }), Update);
            timer.SetInterval(20);

            LoadPreferences();
            this.DataContext = this;
            timer.Start();
        }

        private void LoadPreferences()
        {
            var colorCorrection = Preferences.GetPreference<ColorCorrectionConfig>("settings", "colorCorrection");
            ColorCorrection.GenerateColorCorrectionMap(colorCorrection, out gamma);

            CorrectionEnabled = colorCorrection.CorrectionEnabled;
            GammaCorrection = colorCorrection.GammaCorrection;
            RedCorrection = colorCorrection.RedCorrection;
            GreenCorrection = colorCorrection.GreenCorrection;
            BlueCorrection = colorCorrection.BlueCorrection;

            var arrangement = Preferences.GetPreference<ArrangementConfig>("settings", "arrangement");

            totalLeds = arrangement.top.leds + arrangement.bottom.leds +
                arrangement.left.leds + arrangement.right.leds;

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
            //serialData[10] = (byte)((leds.Length - 1) >> 8);   // LED count high byte
            //serialData[11] = (byte)((leds.Length - 1) & 0xff); // LED count low byte
            //serialData[12] = (byte)(serialData[10] ^ serialData[11] ^ 0x55); // Checksum

            forceUpdate = true;
        }

        private void Update()
        {
            if (!(oldGamma != _gammaCorrection ||
               oldRed != _redCorrection ||
               oldGreen != _greenCorrection ||
               oldBlue != _blueCorrection || 
               oldEnabled != _correctionEnabled ||
               forceUpdate))
                return;

            for (int i = 0; i < 256; i++)
            {
                f = Math.Pow(i / 255.0, _gammaCorrection);
                gamma[i][0] = (byte)(_correctionEnabled ? (f * _redCorrection) : i);
                gamma[i][1] = (byte)(_correctionEnabled ? (f * _greenCorrection) : i);
                gamma[i][2] = (byte)(_correctionEnabled ? (f * _blueCorrection) : i);
            }

            byte correctedRed = gamma[(byte)color.R][0];
            byte correctedGreen = gamma[(byte)color.G][1];
            byte correctedBlue = gamma[(byte)color.B][2];

            int j = 10;
            for (int i = 0; i < totalLeds; i++)
            {
                serialData[j++] = correctedRed;
                serialData[j++] = correctedGreen;
                serialData[j++] = correctedBlue;
            }
            serial.OnDataAvailable(serialData);

            oldGamma = _gammaCorrection;
            oldRed = _redCorrection;
            oldGreen = _greenCorrection;
            oldBlue = _blueCorrection;
            oldEnabled = _correctionEnabled;
        }

        private void Show()
        {
            var a = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                FillBehavior = FillBehavior.HoldEnd,
                BeginTime = TimeSpan.FromSeconds(0),
                Duration = new Duration(TimeSpan.FromSeconds(0.8))
            };
            var storyboard = new Storyboard();

            storyboard.Children.Add(a);
            Storyboard.SetTarget(a, Tab_Main);
            Storyboard.SetTargetProperty(a, new PropertyPath(OpacityProperty));
            storyboard.Begin();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) {
                this.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Show();
            forceUpdate = false;
        }
        #endregion

        private void Sw_CorrectionEnabled_Click(object sender, RoutedEventArgs e)
        {
            Tab_ColorCorrection.IsEnabled = Sw_CorrectionEnabled.IsChecked.Value;
            Tab_ColorCorrection.Opacity = Sw_CorrectionEnabled.IsChecked.Value ? 1.0 : 0.4;
        }

        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            color = e.NewValue.Value;
            var brush = new SolidColorBrush(Color.FromArgb(255, (byte)color.R, (byte)color.G, (byte)color.B));
            Tab_Main.Background = brush;
            forceUpdate = true;
        }

        private bool closing;
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = !closing;

            if (!closing) {
                timer.Stop(() => {
                    this.Dispatcher.Invoke(() => {
                        closing = true;
                        this.Close();
                    });
                });
            }
        }


        private void Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Btn_Save_Click(object sender, RoutedEventArgs e)
        {
            Preferences.SetPreference("settings", "colorCorrection", new ColorCorrectionConfig() { 
                CorrectionEnabled = _correctionEnabled,
                GammaCorrection = _gammaCorrection,
                RedCorrection = _redCorrection,
                GreenCorrection = _greenCorrection,
                BlueCorrection = _blueCorrection
            });

            this.Close();
        }

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
