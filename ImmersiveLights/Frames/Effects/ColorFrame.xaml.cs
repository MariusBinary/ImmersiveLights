using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Collections.Generic;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Core;
using ImmersiveLights.Models;
using ImmersiveLights.Helpers;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Frames
{
    public partial class ColorFrame : UserControl, IEffectCallback
    {
        #region Variables
        private TimerHandler timer;
        private IFrameCallback serial;
        private byte r, g, b, oldR, oldB, oldG;
        private bool firstRun = true;
        private double maxBrightness = 255;
        private double oldBrightness = 255;
        private List<ColorModel> favourites;
        private byte[] serialData;
        private byte[][] gamma;
        private int totalLeds;
        #endregion

        #region Main
        public ColorFrame(IFrameCallback serial)
        {
            InitializeComponent();

            this.serial = serial;
            timer = new TimerHandler();
            timer.Create(new Action(() => { }), OnUpdate);
            timer.SetInterval(32);
            LoadPreferences();
            OnEffectStarted();

            this.DataContext = this;
        }

        private void LoadPreferences()
        {
            bool lightSwitch = Preferences.GetPreference<bool>("settings", "lightSwitch");
            double lightIntensity = Preferences.GetPreference<double>("settings", "lightIntensity");
            maxBrightness = (short)(lightSwitch ? lightIntensity : 0.0);

            favourites = Preferences.GetPreference<List<ColorModel>>("color", "favourites");
            Pussy.ItemsSource = favourites;

            ColorModel selected = Preferences.GetPreference<ColorModel>("color", "selected");
            ColorPicker.SelectedColor = Color.FromRgb(selected.Red, selected.Green, selected.Blue);

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

            var colorCorrection = Preferences.GetPreference<ColorCorrectionConfig>("settings", "colorCorrection");
            ColorCorrection.GenerateColorCorrectionMap(colorCorrection, out gamma);
        }

        private void OnUpdate()
        {
            if (r != oldR || g != oldG || b != oldB || oldBrightness != maxBrightness || firstRun) {

                // Invia il colore ad arduino.
                double scale = maxBrightness / 255;
                int j = 10;
                for (int i = 0; i < totalLeds; i++)
                {
                    serialData[j++] = gamma[(byte)(r * scale)][0];
                    serialData[j++] = gamma[(byte)(g * scale)][1];
                    serialData[j++] = gamma[(byte)(b * scale)][2];
                }
                serial.OnDataAvailable(serialData);

                oldR = r;
                oldG = g;
                oldB = b;
                firstRun = false;
                oldBrightness = maxBrightness;
            }
        }
        #endregion

        /// <summary>
        /// Riceve la chiamata quando un elemento della lista degli effetti viene selezionato
        /// ed effettua a sua volta un'altra chiamata alla funzione che si occupa di attivare
        /// gli effetti, con uno specifico id.
        /// </summary>
        public ICommand ApplyColorCommand => new RelayCommand(item => {
            ColorModel model = item as ColorModel;
            ColorPicker.SelectedColor = Color.FromRgb(model.Red, model.Green, model.Blue);
        });

        /// <summary>
        /// Riceve la chiamata quando un elemento della lista degli effetti viene selezionato
        /// ed effettua a sua volta un'altra chiamata alla funzione che si occupa di attivare
        /// gli effetti, con uno specifico id.
        /// </summary>
        public ICommand DeleteColorCommand => new RelayCommand(item => {
            ColorModel model = item as ColorModel;
            favourites.Remove(model);
            Preferences.SetPreference<List<ColorModel>>("color", "favourites", favourites);

            // Aggiorna la lista
            Pussy.Items.Refresh();
        });

        #region Events
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((Control)sender).Parent as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }

        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            var color = e.NewValue.Value;
            r = color.R;
            g = color.G;
            b = color.B;
        }

        private void ColorPicker_OnFavouriteClick(object sender, RoutedEventArgs e)
        {
            // Controlla che il colore non esista già.
            foreach(ColorModel model in favourites)
            {
                if (model.Red == r && model.Green == g && model.Blue == b) {
                    return;
                }
            } 

            // Salva il colore.
            favourites.Add(new ColorModel() { Red = r, Green = g, Blue = b });
            Preferences.SetPreference<List<ColorModel>>("color", "favourites", favourites);
            Pussy.Items.Refresh();
        }
        #endregion

        #region IEffectCallback
        /// <summary>
        /// Ritorna il tipo di effetto.
        /// </summary>
        public Effects EffectType { get { return Effects.COLOR; } }
        /// <summary>
        /// Indica che le impostazioni dell'effetto hanno subito una modifica
        /// e devono essere ricaricate.
        /// </summary>
        public void OnPreferencesChanged<T>(string key, T value)
        {
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
        /// <summary>
        /// Indica che l'effetto deve fermarsi.
        /// </summary>
        public void OnEffectStopped()
        {
            // Salva l'ultimo colore utilizzato.
            Preferences.SetPreference<ColorModel>("color", "selected", new ColorModel() { 
                Red = r, 
                Green = g, 
                Blue = b 
            });

            timer.Stop(() => {});
        }
        #endregion
    }
}
