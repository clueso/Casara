﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Text;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace Casara
{
    public enum BluetoothConnectionState
    {
        Enumerating,
        Connecting,
        Connected,
        Disconnecting,
        Disconnected
    }

    class BlueToothClass
    {
        private RfcommDeviceService BTService;
        private StreamSocket BTStreamSocket;
        private DataReader BTStreamSocketReader;
        private BluetoothConnectionState BTState;
        private bool display;
        private const int ReadAttemptLength = 64;

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
            BTState = BluetoothConnectionState.Disconnected;
            display = false;
        }

        public void StartDisconnectProcess()
        {
            BTState = BluetoothConnectionState.Disconnecting;
        }

        public void StartDataDisplay()
        {
            display = true;
        }

        public void StopDataDisplay()
        {
            display = false;
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
            uint BytesReturned;

            while (BTStreamSocketReader != null && BTState != BluetoothConnectionState.Disconnecting)
            {
                try
                {
                    BytesReturned = await BTStreamSocketReader.LoadAsync(ReadAttemptLength);
                    if (BytesReturned == 0)
                        return;
                    // Read the message and process it.
                    string message = BTStreamSocketReader.ReadString(BytesReturned);
                    if (display)
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
                BTStreamSocketReader = null;
 
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
