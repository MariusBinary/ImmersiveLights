using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using ImmersiveLights.Core;
using ImmersiveLights.Pages;
using ImmersiveLights.Interfaces;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System;

namespace ImmersiveLights.Frames
{
    public partial class HomeFrame : UserControl, IFrameMetadata, INotifyPropertyChanged
    {
        #region IFrameCallback
        public string Title { get { return FindResource("navbarHome") as string; } }
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
            if (status == ConnectionStatus.CONNECTED) {
                Tab_Main.IsEnabled = true;
                Tab_Main.Opacity = 1.0;
                if (canUpdateEffect) {
                    UpdateEffect(defaultEffect);
                }
            } else {
                Tab_Main.IsEnabled = false;
                Tab_Main.Opacity = 0.4;
            }
        }
        #endregion

        #region Properties
        private object _frameContent;
        public object FrameContent
        {
            get { return _frameContent; }
            set
            {
                if (_frameContent != value)
                {
                    _frameContent = value;
                    RaisePropertyChanged("FrameContent");
                }
            }
        }

        private bool _lightSwitch;
        public bool LightSwitch
        {
            get { return _lightSwitch; }
            set
            {
                _lightSwitch = value;
                RaisePropertyChanged("LightSwitch");

                if (currentEffect != Effects.NONE) {
                    (_frameContent as IEffectCallback).OnBrightnessChanged(value ? _lightIntensity : 0.0);
                }
            }
        }

        private double _lightIntensity;
        public double LightIntensity
        {
            get { return _lightIntensity; }
            set
            {
                _lightIntensity = value;
                RaisePropertyChanged("LightIntensity");

                if (currentEffect != Effects.NONE && _lightSwitch) {
                    (_frameContent as IEffectCallback).OnBrightnessChanged(value);
                }
            }
        }
        #endregion

        #region Variables
        private Effects currentEffect = Effects.NONE;
        private Effects defaultEffect = Effects.NONE;
        private bool isEffectUpdating = false;
        private bool canUpdateEffect = false;
        #endregion

        public HomeFrame()
        {
            InitializeComponent();

            // Load preferences.
            LoadPreferences();

            // Reload an effect if it's currently running.
            var mainWindow = ((MainWindow)Application.Current.MainWindow);
            OnConnectionChanged(mainWindow.serialHandler.connectionStatus);
            var currentEffect = mainWindow.currentEffect;
            var currentEffectControl = mainWindow.currentEffectControl;
            if (mainWindow.serialHandler.connectionStatus == ConnectionStatus.CONNECTED) {
                if (currentEffect != Effects.NONE) {
                    UpdateEffect(currentEffect, currentEffectControl);
                } else {
                    UpdateEffect(defaultEffect);
                }
            }

            this.canUpdateEffect = true;
            this.DataContext = this;
        }

        /// <summary>
        /// Carica le preferenze dell'utente.
        /// </summary>
        public void LoadPreferences()
        {
            LightSwitch = Preferences.GetPreference<bool>("settings", "lightSwitch");
            LightIntensity = Preferences.GetPreference<double>("settings", "lightIntensity");
            defaultEffect = Preferences.GetPreference<Effects>("general", "selectedEffect");
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            Preferences.SetPreference<bool>("settings", "lightSwitch", _lightSwitch);
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            Preferences.SetPreference<double>("settings", "lightIntensity", _lightIntensity);
        }

        #region Navigation
        /// <summary>
        /// Cambia l'effetto in esecuzione.
        /// </summary>
        public void UpdateEffect(Effects effect, 
            UserControl control = null, bool notifyOnUpdate = false, Action onEffectUpdated = null)
        {
            // Se l'effetto che si cerca di impostare è uguale a quello
            // già in esecuzione, non andare avanti.
            if (effect == currentEffect || isEffectUpdating) return;

            isEffectUpdating = true;

            // Arresta il vecchio effetto.
            int sleepDelay = 0;
            if (currentEffect != Effects.NONE) {
                (FrameContent as IEffectCallback).OnEffectStopped();
                Tab_FrameContent.Visibility = Visibility.Collapsed;
                Tab_Spinner.Visibility = Visibility.Visible;
                sleepDelay = 250;
            }

            Task.Delay(sleepDelay).ContinueWith(t => {
                var mainWindow = ((MainWindow)Application.Current.MainWindow);
                var serialHandler = mainWindow.serialHandler;
                bool isInitRequired = (control == null);
                Console.WriteLine("isInitRequired: " + isInitRequired);
                // Aggiorna lo stato dei pulsanti e imposta il contenuto
                // dell'effetto, inizializzandolo se necessario.
                switch (effect)
                {
                    case Effects.NONE:
                        Btn_EffectColor.IsChecked = false;
                        Btn_EffectScenes.IsChecked = false;
                        Btn_EffectMusic.IsChecked = false;
                        Btn_EffectAmbilight.IsChecked = false;
                        FrameContent = null;
                        break;
                    case Effects.COLOR:
                        Btn_EffectColor.IsChecked = true;
                        FrameContent = isInitRequired ?
                            new ColorFrame(serialHandler) : control;
                        break;
                    case Effects.SCENES:
                        Btn_EffectScenes.IsChecked = true;
                        FrameContent = isInitRequired ?
                            new ScenesFrame(serialHandler) : control;
                        break;
                    case Effects.MUSIC:
                        Btn_EffectMusic.IsChecked = true;
                        FrameContent = isInitRequired ?
                            new MusicFrame(serialHandler) : control;
                        break;
                    case Effects.AMBILIGHT:
                        Btn_EffectAmbilight.IsChecked = true;
                        FrameContent = isInitRequired ?
                            new AmbilightFrame(serialHandler) : control;
                        break;
                }

                currentEffect = effect;
                mainWindow.currentEffect = currentEffect;
                mainWindow.currentEffectControl = FrameContent as UserControl;

                Tab_Spinner.Visibility = Visibility.Collapsed;
                Tab_FrameContent.Visibility = Visibility.Visible;
                isEffectUpdating = false;

                if (notifyOnUpdate) {
                    onEffectUpdated?.Invoke();
                } else {
                    if (effect != Effects.NONE) {
                        Preferences.SetPreference<Effects>("general", "selectedEffect", effect);
                        defaultEffect = effect;
                    }
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Btn_EffectColor_Click(object sender, RoutedEventArgs e)
        {
            UpdateEffect(Effects.COLOR);
        }

        private void Btn_EffectScenes_Click(object sender, RoutedEventArgs e)
        {
            UpdateEffect(Effects.SCENES);
        }

        private void Btn_EffectMusic_Click(object sender, RoutedEventArgs e)
        {
            UpdateEffect(Effects.MUSIC);
        }

        private void Btn_EffectAmbilight_Click(object sender, RoutedEventArgs e)
        {
            UpdateEffect(Effects.AMBILIGHT);
        }
        #endregion

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
