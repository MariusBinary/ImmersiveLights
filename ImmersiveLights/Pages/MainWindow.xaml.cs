using System;
using System.Windows;
using System.Linq;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using ImmersiveLights.Core;
using ImmersiveLights.Settings;
using ImmersiveLights.Frames;
using ImmersiveLights.Controls;
using ImmersiveLights.Interfaces;

namespace ImmersiveLights.Pages
{
    public enum NavigationPages
    {
        NONE, HOME, CONNECTION, CAPTURE_PREFERENCES, 
        AUDIO_PREFERENCES, SYSTEM, UPDATE, ABOUT,
    }

    public partial class MainWindow : Window, ISerialResponse, INotifyPropertyChanged
    {
        #region Variables
        private int sizeStatus = 0;
        private bool isNavbarOpen = false;
        private bool canClose = false;
        private bool tryToClose = false;
        private bool isArrangementEditorForced;
        private bool shouldStartInBackground;
        public SerialHandler serialHandler;
        public NavigationPages currentPage;
        public Effects currentEffect;
        public UserControl currentEffectControl;
        public bool isConnectionChanging = false;
        #endregion

        #region Properties
        private object _frameContent;
        public object FrameContent
        {
            get { return _frameContent; }
            set
            {
                _frameContent = value;
                RaisePropertyChanged("FrameContent");
            }
        }
        #endregion

        #region Main
        public MainWindow()
        {
            InitializeComponent();

            this.serialHandler = new SerialHandler(this);
            this.DataContext = this;

            // Ottiene i parametri di avvio.
            string[] args = Environment.GetCommandLineArgs();

            // Verifica sono presenti parametri di avvio validi.
            if (args.Length > 1)
            {
                if (args[1].Equals("-autorun"))
                {
                    // Spostare il programma in background in fase di avvio automatico se necessario.
                    bool shouldWindowBeMinimized = Core.Preferences.GetPreference<bool>("settings", "startReducedToIcon");
                    if (shouldWindowBeMinimized)
                    {
                        this.shouldStartInBackground = true;
                        this.ShowInTaskbar = false;
                        this.Hide();
                    }
                }
            }
        }

        /// <summary>
        /// Gestisce il caricamento della finestra.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (shouldStartInBackground)
            {
                Task.Delay(5000).ContinueWith(task =>
                {
                    // Cerca di eseguire il collegamento con la schedina.
                    int attempts = 0;
                    int maxAttempts = Core.Preferences.GetPreference<int>("settings", "startAttempts");
                    while (attempts < maxAttempts && serialHandler.connectionStatus == ConnectionStatus.DISCONNECTED)
                    {
                        Btn_Connect_Click(null, null);
                        Task.Delay(2500);
                        attempts += 1;
                    }

                    NavigateTo(NavigationPages.HOME);
                });
            } 
            else 
            {
                NavigateTo(NavigationPages.HOME);
            }
        }

        /// <summary>
        /// Gestisce il ridimensionamento della finestra.
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Aggiorna lo stato del menu
            MenuTo(this.ActualWidth <= 765 ? 0 : 1);
        }

        /// <summary>
        /// Gestisce la chiusura della finestra.
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = !canClose;

            // Verifica l'azione da eseguire alla pressione del pulsante di chiusura
            var shouldReduceAsIconTry = Preferences.GetPreference<bool>("settings", "reduceAsIconTry");
            if (shouldReduceAsIconTry && tryToClose != true) {
                TBar_Icon.ShowBalloonTip(
                    FindResource("balloonTipRunningInBackgroundTitle") as string, 
                    FindResource("balloonTipRunningInBackgroundDescription") as string, 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                this.ShowInTaskbar = false;
                this.Hide();
                return;
            } 

            if (!canClose) {
                tryToClose = true;
                if (serialHandler.connectionStatus == ConnectionStatus.CONNECTED) {
                    Btn_Connect_Click(null, null);
                } else {
                    e.Cancel = false;
                }
            }
        }
        #endregion

        #region Events
        private void Btn_Connect_Click(object sender, RoutedEventArgs e)
        {
            // Se il tasto è stato già premuto precedentemente e l'azione
            // non è stata ancora completata, non continuare.
            if (isConnectionChanging) return;

            // Indica che il stato è stato premuto.
            isConnectionChanging = true;

            if (serialHandler.connectionStatus == ConnectionStatus.DISCONNECTED)
            {
                // Se il dispositivo è attualemente disconnesso, eseguire la connessione.
                var connectionType = Preferences.GetPreference<ConnectionType>("connection", "connectionType");
                var param1 = Preferences.GetPreference<string>("connection", 
                    connectionType == ConnectionType.USB ? "connectionUsbParam1" : "connectionLanParam1");
                var param2 = Preferences.GetPreference<int>("connection",
                    connectionType == ConnectionType.USB ? "connectionUsbParam2" : "connectionLanParam2");

                if (connectionType == ConnectionType.VIRTUAL || !String.IsNullOrEmpty(param1)) {
                    serialHandler.Connect(connectionType, param1, param2);
                } else {
                    if (!shouldStartInBackground) {
                        WpfMessageBox.Show(FindResource("alertConnectionErrorTitle") as string, 
                            FindResource("alertConnectionErrorDescription") as string);
                    }
                    isConnectionChanging = false;
                }
            }
            else
            {
                // Se il dispositivo è attualmente connesso, interromere l'esecuzione 
                // dell'effetto corrente e poi eseguire la disconnessione.
                if (currentPage == NavigationPages.HOME)
                {
                    (_frameContent as HomeFrame).UpdateEffect(Effects.NONE, null, true, () => {
                        serialHandler.Disconnect();
                    });
                }
                else
                {
                    int sleepDelay = 0;
                    if (currentEffect != Effects.NONE) {
                        (currentEffectControl as IEffectCallback).OnEffectStopped();
                        sleepDelay = 250;
                    }

                    Task.Delay(sleepDelay).ContinueWith(t => {
                        currentEffect = Effects.NONE;
                        currentEffectControl = null;
                        serialHandler.Disconnect();
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }
        #endregion

        #region Navigation
        /// <summary>
        /// Naviga a una specifica pagina.
        /// </summary>
        public void NavigateTo(NavigationPages page)
        {
            if (page == currentPage) return;
            
            switch(page)
            {
                case NavigationPages.HOME:
                    FrameContent = new HomeFrame();
                    Btn_NavHome.IsChecked = true;
                    break;
                case NavigationPages.CONNECTION:
                    FrameContent = new ConnectionFrame();
                    Btn_NavConnection.IsChecked = true;
                    break;
                case NavigationPages.CAPTURE_PREFERENCES:
                    FrameContent = new ScreenCaptureFrame();
                    Btn_NavCapture.IsChecked = true;
                    break;
                case NavigationPages.AUDIO_PREFERENCES:
                    FrameContent = new AudioCaptureFrame();
                    Btn_NavAudio.IsChecked = true;
                    break;
                case NavigationPages.SYSTEM:
                    FrameContent = new SystemFrame();
                    Btn_NavSystem.IsChecked = true;
                    break;
                case NavigationPages.UPDATE:
                    FrameContent = new UpdateFrame();
                    Btn_NavUpdate.IsChecked = true;
                    break;
                case NavigationPages.ABOUT:
                    FrameContent = new AboutFrame();
                    Btn_NavAbout.IsChecked = true;
                    break;
            }

            var frameMetadata = (FrameContent as IFrameMetadata);
            Tb_ContentTitle.Text = frameMetadata == null ? "Home" : frameMetadata.Title;
            currentPage = page;
        }

        /// <summary>
        /// Aggiorna lo stato del menu di navigazione e ne cambia il contenitore.
        /// </summary>
        private void MenuTo(int index)
        {
            switch (index)
            {
                case 0:
                    if (sizeStatus == 0)
                    {
                        Tab_ContentHeader.ColumnDefinitions[0].Width = new GridLength(64, GridUnitType.Pixel);
                        Tab_Root.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Pixel);
                        Tab_Root.ColumnDefinitions[0].MinWidth = 0;
                        Tab_Root.ColumnDefinitions[0].MaxWidth = 0;
                        Btn_NavToggle.Visibility = Visibility.Visible;

                        var childrenList = Tab_Sidebar.Children.Cast<UIElement>().ToArray();
                        Tab_PopupMenu.Children.Clear();
                        foreach (var c in childrenList)
                        {
                            Tab_Sidebar.Children.Remove(c);
                            Tab_PopupMenu.Children.Add(c);
                        }

                        sizeStatus = 1;
                    }
                    break;
                case 1:
                    if (sizeStatus == 1)
                    {
                        Tab_ContentHeader.ColumnDefinitions[0].Width = new GridLength(20, GridUnitType.Pixel);
                        Tab_Root.ColumnDefinitions[0].Width = new GridLength(0.3, GridUnitType.Star);
                        Tab_Root.ColumnDefinitions[0].MinWidth = 280;
                        Tab_Root.ColumnDefinitions[0].MaxWidth = 380;
                        Btn_NavToggle.Visibility = Visibility.Collapsed;

                        if (isNavbarOpen)
                        {
                            Tab_PopupMenu.Visibility = Visibility.Collapsed;
                            Tab_Overlay.Visibility = Visibility.Collapsed;
                        }

                        var childrenList = Tab_PopupMenu.Children.Cast<UIElement>().ToArray();
                        Tab_Sidebar.Children.Clear();
                        foreach (var c in childrenList)
                        {
                            Tab_PopupMenu.Children.Remove(c);
                            Tab_Sidebar.Children.Add(c);
                        }

                        sizeStatus = 0;
                    }
                    break;

            }
        }

        private void Btn_NavToggle_Click(object sender, RoutedEventArgs e)
        {
            Tab_PopupMenu.Visibility = Tab_PopupMenu.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            Tab_Overlay.Visibility = Tab_PopupMenu.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
            isNavbarOpen = Tab_Overlay.Visibility == Visibility.Visible ? true : false;
        }

        private void Tab_Overlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Tab_PopupMenu.Visibility = Tab_PopupMenu.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            Tab_Overlay.Visibility = Tab_PopupMenu.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
            isNavbarOpen = Tab_Overlay.Visibility == Visibility.Visible ? true : false;
        }

        private void Btn_NavHome_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.HOME);
        }

        private void Btn_NavConnection_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.CONNECTION);
        }

        private void Btn_NavLayout_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e != null) {
                e.Handled = true;
            }

            //NavigateTo(NavigationPages.ARRANGEMENT);
            if (serialHandler.connectionStatus == ConnectionStatus.DISCONNECTED) {
                new ArrangementWizard().ShowDialog();
            } else {
                WpfMessageBox.Show(FindResource("alertArrangementDenialTitle") as string,
                    FindResource("alertArrangementDenialDescription") as string);
            }
        }

        private void Btn_NavLayout_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Btn_NavCorrection_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (serialHandler.connectionStatus == ConnectionStatus.CONNECTED) {

                // Se il dispositivo è attualmente connesso, interromere l'esecuzione 
                // dell'effetto corrente e poi eseguire la disconnessione.
                if (currentPage == NavigationPages.HOME)
                {
                    (_frameContent as HomeFrame).UpdateEffect(Effects.NONE, null, true, () => {
                        new ColorCorrectionWizard(serialHandler).ShowDialog();
                        ReloadEffect();
                    });
                }
                else
                {
                    int sleepDelay = 0;
                    if (currentEffect != Effects.NONE) {
                        (currentEffectControl as IEffectCallback).OnEffectStopped();
                        sleepDelay = 250;
                    }

                    Task.Delay(sleepDelay).ContinueWith(t => {
                        currentEffect = Effects.NONE;
                        currentEffectControl = null;
                        new ColorCorrectionWizard(serialHandler).ShowDialog();
                        ReloadEffect();
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
            } else {
                WpfMessageBox.Show(FindResource("alertColorCorrectionDenialTitle") as string,
                    FindResource("alertColorCorrectionDenialDescription") as string);
            }
        }

        private void Btn_NavCorrection_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Btn_NavCapture_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.CAPTURE_PREFERENCES);
        }

        private void Btn_NavAudio_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.AUDIO_PREFERENCES);
        }

        private void Btn_NavSystem_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.SYSTEM);
        }

        private void Btn_NavUpdate_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.UPDATE);
        }

        private void Btn_NavAbout_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationPages.ABOUT);
        }
        #endregion

        #region Helpers
        private void ReloadEffect()
        {
            // Quando viene eseguita la connessione, si richiede che l'utente
            // venga sempre portato sulla pagina degli effetti affinchè questo
            // si possa avviare.
            if (currentPage != NavigationPages.HOME) {
                NavigateTo(NavigationPages.HOME);
            } else if (_frameContent != null) {
                (_frameContent as IFrameMetadata).OnConnectionChanged(ConnectionStatus.CONNECTED);
            }
        }

        public void ForceArrangementEditor()
        {
            if (serialHandler.connectionStatus == ConnectionStatus.CONNECTED) {
                isArrangementEditorForced = true;
                Btn_Connect_Click(null, null);
            }
        }

        public void ToggleConnection()
        {
            Btn_Connect_Click(null, null);
        }
        #endregion

        #region ISerialResponse 
        /// <summary>
        /// Indica che la scheda è stata collegata con successo.
        /// </summary>
        public void OnBoardConnect() {
            (Btn_Connect.Content as TextBlock).Text = FindResource("disconnectButton") as string;
            Btn_Connect.Background = new SolidColorBrush(Color.FromArgb(255, 231, 53, 53));

            // Quando viene eseguita la connessione, si richiede che l'utente
            // venga sempre portato sulla pagina degli effetti affinchè questo
            // si possa avviare.
            if (currentPage != NavigationPages.HOME) {
                NavigateTo(NavigationPages.HOME);
            } else if (_frameContent != null) {
                (_frameContent as IFrameMetadata).OnConnectionChanged(ConnectionStatus.CONNECTED);
            }

            isConnectionChanging = false;
            canClose = false;
        }
        /// <summary>
        /// Indica che la scheda è stata scollegata con successo.
        /// </summary>
        public void OnBoardDisconnect() {
            (Btn_Connect.Content as TextBlock).Text = FindResource("connectButton") as string;
            Btn_Connect.Background = new SolidColorBrush(Color.FromArgb(255, 124, 204, 6));

            if (currentPage != NavigationPages.NONE && _frameContent != null) {
                (_frameContent as IFrameMetadata).OnConnectionChanged(ConnectionStatus.DISCONNECTED);
            }

            isConnectionChanging = false;
            canClose = true;

            if (isArrangementEditorForced) {
                Btn_NavLayout_PreviewMouseLeftButtonUp(null, null);
                isArrangementEditorForced = false;
            }

            if (tryToClose) {
                this.Close();
            }
        }
        /// <summary>
        /// Indica che il collegamento con la scheda è stato interrotto
        /// per un errore del programma o disconnessione fisica.
        /// </summary>
        public void OnBoardError() {
            WpfMessageBox.Show(FindResource("alertConnectionErrorTitle") as string,
                FindResource("alertConnectionErrorDescription") as string);
            isConnectionChanging = false;
        }
        #endregion

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        #region Taskbar Controls
        private void TBar_Icon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            this.ShowInTaskbar = true;
            this.Show();
        }

        private void Menu_Open_Click(object sender, RoutedEventArgs e)
        {
            this.ShowInTaskbar = true;
            this.Show();
        }

        private void Menu_Home_Click(object sender, RoutedEventArgs e)
        {
            Menu_Open_Click(null, null);
            NavigateTo(NavigationPages.HOME);
        }

        private void Menu_Settings_Click(object sender, RoutedEventArgs e)
        {
            Menu_Open_Click(null, null);
            NavigateTo(NavigationPages.SYSTEM);
        }

        private void Menu_CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            Menu_Open_Click(null, null);
            NavigateTo(NavigationPages.UPDATE);
        }

        private void Menu_About_Click(object sender, RoutedEventArgs e)
        {
            Menu_Open_Click(null, null);
            NavigateTo(NavigationPages.ABOUT);
        }

        private void Menu_Ouit_Click(object sender, RoutedEventArgs e)
        {
            tryToClose = true;
            this.Close();
        }
        #endregion
    }
}
