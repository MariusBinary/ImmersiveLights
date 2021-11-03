using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO.Ports;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Core;
using ImmersiveLights.Pages;

namespace ImmersiveLights.Settings
{
    public partial class ConnectionFrame : UserControl, IFrameMetadata
    {
        #region IFrameMetadata
        public string Title { get { return FindResource("navbarConnection") as string; } }
        public double InitialHeight { get { return 538.0; } }

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
                ChangeTabEnabled(Tab_Main, false);
            } else {
                ChangeTabEnabled(Tab_Main, true);
            }
        }
        #endregion

        #region Variables
        private bool isUserAllowed;
        #endregion

        #region Main
        public ConnectionFrame()
        {
            InitializeComponent();

            var mainWindow = (MainWindow)Application.Current.MainWindow;
            if (mainWindow.serialHandler.connectionStatus == ConnectionStatus.CONNECTED) {
                ChangeTabEnabled(Tab_Main, false);
            } else {
                ChangeTabEnabled(Tab_Main, true);
            }

            LoadPreferences();
        }

        private void LoadPreferences()
        {
            CBox_ConnectionType.SelectedIndex = (int)(Preferences.GetPreference<ConnectionType>("connection", "connectionType")) - 1;

            // Get a list of serial port names.
            string[] ports = SerialPort.GetPortNames();

            Cbox_UsbConnectionParam1.ItemsSource = ports;
            Cbox_UsbConnectionParam1.SelectedValue = Preferences.GetPreference<string>("connection", "connectionUsbParam1");
            if (Cbox_UsbConnectionParam1.SelectedIndex == -1 && ports.Length > 0) {
                Cbox_UsbConnectionParam1.SelectedIndex = 0;
                Preferences.SetPreference<string>("connection", "connectionUsbParam1", ports[0]);
            }

            Tb_UsbConnectionParam2.Text = Preferences.GetPreference<int>("connection", "connectionUsbParam2").ToString();

            Tb_LanConnectionParam1.Text = Preferences.GetPreference<string>("connection", "connectionLanParam1");
            Tb_LanConnectionParam2.Text = Preferences.GetPreference<int>("connection", "connectionLanParam2").ToString();

            isUserAllowed = true;
        }
        #endregion

        #region Events
        private void CBox_ConnectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch(CBox_ConnectionType.SelectedIndex)
            {
                case 0:
                    Tab_LanConnection.Visibility = Visibility.Collapsed;
                    Tab_UsbConnection.Visibility = Visibility.Visible;
                    break;
                case 1:
                    Tab_UsbConnection.Visibility = Visibility.Collapsed;
                    Tab_LanConnection.Visibility = Visibility.Visible;
                    break;
                case 2:
                    Tab_UsbConnection.Visibility = Visibility.Collapsed;
                    Tab_LanConnection.Visibility = Visibility.Collapsed;
                    break;
            }

            if (!isUserAllowed) return;
            Preferences.SetPreference<ConnectionType>("connection", "connectionType", (ConnectionType)(CBox_ConnectionType.SelectedIndex + 1));
        }

        // Usb parameters.
        private void Cbox_UsbConnectionParam1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isUserAllowed) return;
            Preferences.SetPreference<string>("connection", "connectionUsbParam1", (string)Cbox_UsbConnectionParam1.SelectedValue);
        }

        private void Tb_UsbConnectionParam2_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!isUserAllowed) return;

            char c = Convert.ToChar(e.Text);
            if (Char.IsNumber(c))
                e.Handled = false;
            else
                e.Handled = true;

            base.OnPreviewTextInput(e);
        }

        private void Tb_UsbConnectionParam2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUserAllowed) return;
            Preferences.SetPreference<int>("connection", "connectionUsbParam2", Convert.ToInt32(Tb_UsbConnectionParam2.Text));
        }

        // Lan parameters.
        private void Tb_LanConnectionParam1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUserAllowed) return;
            Preferences.SetPreference<string>("connection", "connectionLanParam1", Tb_LanConnectionParam1.Text);
        }

        private void Tb_LanConnectionParam2_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!isUserAllowed) return;

            char c = Convert.ToChar(e.Text);
            if (Char.IsNumber(c))
                e.Handled = false;
            else
                e.Handled = true;

            base.OnPreviewTextInput(e);
        }

        private void Tb_LanConnectionParam2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isUserAllowed) return;
            Preferences.SetPreference<int>("connection", "connectionLanParam2", Convert.ToInt32(Tb_LanConnectionParam2.Text));
        }
        #endregion

        #region Helpers
        private void ChangeTabEnabled(UIElement tab, bool enabled)
        {
            tab.IsEnabled = enabled;
            tab.Opacity = enabled ? 1.0 : 0.4;
        }
        #endregion
    }
}
