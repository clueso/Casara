using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Text;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace Casara
{
    public enum BluetoothConnectionState
    {
        Disconnected,
        Connected,
        Enumerating,
        Connecting
    }

    class BlueToothClass
    {
        private RfcommDeviceService BTService;
        private StreamSocket BTStreamSocket;
        private DataReader BTStreamSocketReader;
        private DataWriter BTStreamSocketWriter;
        private BluetoothConnectionState BTState; 

        public delegate void AddOnExceptionOccuredDelegate(object sender, Exception ex);
        public event AddOnExceptionOccuredDelegate ExceptionOccured;
        private void OnExceptionOccuredEvent(object sender, Exception ex)
        {
            if (ExceptionOccured != null)
                ExceptionOccured(sender, ex);
        }

        //OnMessageReceived
        public delegate void AddOnDataReceivedDelegate(object sender, string message);
        public event AddOnDataReceivedDelegate MessageReceived;
        
        private void OnMessageReceivedEvent(object sender, string message)
        {
            if (MessageReceived != null)
                MessageReceived(sender, message);
        }

        public BlueToothClass()
        {
            BTService = null;
            BTStreamSocket = null;
            BTStreamSocketReader = null;
            BTStreamSocketWriter = null;
            BTState = BluetoothConnectionState.Disconnected;
        }

        public bool IsBTConnected
        {
            get
            {
                return (BTState == BluetoothConnectionState.Connected);
            }
        }

        public async Task<DeviceInformationCollection> EnumerateDevices(RfcommServiceId ServiceId)
        {
            this.BTState = BluetoothConnectionState.Enumerating;    //Maybe we don't need this because it is a blocking call...
            DeviceInformationCollection ConnectedDevices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                                RfcommDeviceService.GetDeviceSelector(ServiceId));
            return ConnectedDevices;
        }

        public async Task ConnectDevice(DeviceInformation ChosenDevice)
        {
            try
            {
                BTService = await RfcommDeviceService.FromIdAsync(ChosenDevice.Id);
                if (BTService != null)
                {
                    // Create a socket and connect to the target 
                    BTStreamSocket = new StreamSocket();
                    await BTStreamSocket.ConnectAsync(BTService.ConnectionHostName, BTService.ConnectionServiceName,
                                                      SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);
                    BTStreamSocketWriter = new DataWriter(BTStreamSocket.OutputStream);
                    BTStreamSocketReader = new DataReader(BTStreamSocket.InputStream);
                    BTStreamSocketReader.ByteOrder = ByteOrder.LittleEndian;
                    BTStreamSocketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    this.BTState = BluetoothConnectionState.Connected;
                }
                else
                    OnExceptionOccuredEvent(this, new Exception("Unable to create service.\nMake sure that the 'bluetooth.rfcomm' capability is declared with a function of type 'name:serialPort' in Package.appxmanifest."));
            }
            catch (Exception ex)
            {
                this.BTState = BluetoothConnectionState.Disconnected;
                OnExceptionOccuredEvent(this, ex);
            }
        }

        public async Task ListenForData()
        {
            while (BTStreamSocketReader != null)
            {
                try
                {
                    // Read first byte (length of the subsequent message, 255 or less). 
                    uint sizeFieldCount = await BTStreamSocketReader.LoadAsync(1);
                    if (sizeFieldCount != 1)
                    {
                        // The underlying socket was closed before we were able to read the whole data. 
                        return;
                    }

                    // Read the message. 
                    uint messageLength = BTStreamSocketReader.ReadByte();
                    uint actualMessageLength = await BTStreamSocketReader.LoadAsync(messageLength);
                    if (messageLength != actualMessageLength)
                    {
                        // The underlying socket was closed before we were able to read the whole data. 
                        return;
                    }
                    // Read the message and process it.
                    string message = BTStreamSocketReader.ReadString(actualMessageLength);
                    OnMessageReceivedEvent(this, message);
                }
                catch (Exception ex)
                {
                    if (BTStreamSocketReader != null)
                        OnExceptionOccuredEvent(this, ex);
                }
            }
        }

        public void DisconnectDevice()
        {
            if (BTStreamSocketReader != null)
            {
                //BTStreamSocketReader.DetachStream();
                //BTStreamSocketReader.Dispose();
                BTStreamSocketReader = null;
            }
                

            if (BTStreamSocketWriter != null)
            {
                BTStreamSocketWriter.DetachStream();
                BTStreamSocketWriter.Dispose();
                BTStreamSocketWriter = null;
            }
                
            if (BTStreamSocket != null)
            {
                BTStreamSocket.Dispose();
                BTStreamSocket = null;
            }

            if (BTService != null)
                BTService = null;

            this.BTState = BluetoothConnectionState.Disconnected;
        }
    }
}
