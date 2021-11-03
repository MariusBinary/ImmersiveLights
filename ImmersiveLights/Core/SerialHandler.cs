using System;
using System.Windows;
using System.Net.Sockets;
using System.IO.Ports;
using ImmersiveLights.Pages;
using ImmersiveLights.Interfaces;

namespace ImmersiveLights.Core
{
    public enum ConnectionType
    {
        NONE, USB, LAN, VIRTUAL
    }

    public enum ConnectionStatus
    {
        DISCONNECTED, CONNECTED
    }

    public class SerialHandler : IFrameCallback
    {
        #region Variables
        private Window uiContext;
        private ISerialResponse serialInterface;
        private SafeSerialPort serialPort;
        private UdpClient udpClient;
        public ConnectionType connectionType;
        public ConnectionStatus connectionStatus;
        public VirtualScreen virtualScreen;
        #endregion

        #region Main
        /// <summary>
        /// Punto di ingresso dell gestore.
        /// </summary>
        public SerialHandler(Window uiContext)
        {
            this.uiContext = uiContext;
            this.serialInterface = uiContext as ISerialResponse;
            this.connectionType = ConnectionType.NONE;
            this.connectionStatus = ConnectionStatus.DISCONNECTED;
        }
        #endregion

        #region Controls
        /// <summary>
        /// Effettua una connessione con attraverso la porta seriale indicata.
        /// </summary>
        public void Connect(ConnectionType connectionType, string param1, int param2)
        {
            if (connectionStatus == ConnectionStatus.CONNECTED) return;

            try
            {
                // Aggiorna il tipo di connesione.
                this.connectionType = connectionType;

                if (connectionType == ConnectionType.USB)
                {
                    // Avvia la connessione di tipo USB.
                    serialPort = new SafeSerialPort {
                        PortName = param1,
                        BaudRate = param2,
                        Parity = Parity.None,
                        DataBits = 8,
                        StopBits = StopBits.One
                    };
                    serialPort.Open();
                }
                else if (connectionType == ConnectionType.LAN)
                {
                    // Avvia la connessione di tipo LAN.
                    udpClient = new UdpClient();
                    udpClient.Connect(param1, param2);
                }
                else if (connectionType == ConnectionType.VIRTUAL)
                {
                    // Avvia la connessione di tipo VIRTUAL.
                    this.virtualScreen = new VirtualScreen();
                    this.virtualScreen.Show();
                }

                connectionStatus = ConnectionStatus.CONNECTED;
                uiContext.Dispatcher.Invoke(new Action(delegate {
                    serialInterface.OnBoardConnect();
                }));
            }
            catch
            {
                connectionStatus = ConnectionStatus.DISCONNECTED;
                uiContext.Dispatcher.Invoke(new Action(delegate
                {
                    serialInterface.OnBoardError();
                }));
            }
        }
        /// <summary>
        /// Effettua una disconessione e un rilascio della porta seriale.
        /// </summary>
        public void Disconnect()
        {
            if (connectionStatus == ConnectionStatus.DISCONNECTED) return;

            try
            {
                if (connectionType == ConnectionType.USB)
                {
                    // Chiude la connessione di tipo USB.
                    serialPort.Close();
                    serialPort.Dispose();
                }
                else if (connectionType == ConnectionType.LAN)
                {
                    // Chiude la connessione di tipo LAN.
                    udpClient.Close();
                    udpClient.Dispose();
                }
                else if (connectionType == ConnectionType.VIRTUAL)
                {
                    // Chiude la connessione di tipo VIRTUAL.
                    virtualScreen.Close();
                }

                connectionStatus = ConnectionStatus.DISCONNECTED;
                uiContext.Dispatcher.Invoke(new Action(delegate {
                    serialInterface.OnBoardDisconnect();
                }));
            }
            catch
            {
                connectionStatus = ConnectionStatus.DISCONNECTED;
                uiContext.Dispatcher.Invoke(new Action(delegate {
                    serialInterface.OnBoardError();
                }));
            }
        }
        #endregion

        #region Write
        /// <summary>
        /// Scrive sulla porta seriale l'array di byte ricevuto.
        /// </summary>
        public void OnDataAvailable(byte[] data)
        {
            if (connectionStatus == ConnectionStatus.DISCONNECTED) return;

            try
            {
                if (connectionType == ConnectionType.USB)
                {
                    // Invia i dati sulla connessione di tipo USB.
                    serialPort.Write(data, 0, data.Length);
                }
                else if (connectionType == ConnectionType.LAN)
                {
                    // Invia i dati sulla connessione di tipo LAN.
                    udpClient.Send(data, data.Length);
                }
                else if (connectionType == ConnectionType.VIRTUAL)
                {
                    // Invia i dati sulla connessione di tipo VIRTUAL.
                    virtualScreen.Update(data);
                }
            }
            catch (Exception ex)
            {
                uiContext.Dispatcher.Invoke(new Action(delegate {
                    if (connectionStatus == ConnectionStatus.CONNECTED) {
                        (uiContext as MainWindow).ToggleConnection();
                    }
                }));
            }
        }
        #endregion
    }
}
