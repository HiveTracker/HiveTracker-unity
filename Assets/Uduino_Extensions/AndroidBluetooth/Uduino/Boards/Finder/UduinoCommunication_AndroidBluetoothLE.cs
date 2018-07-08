using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;

namespace Uduino
{
    public class UduinoCommunication_AndroidBluetoothLE : MonoBehaviour
    {
        public UduinoConnection_AndroidBluetoothLE connection = null;
        public static UduinoCommunication_AndroidBluetoothLE Instance = null;
        public string id = null;

        public void Awake()
        {
            Instance = this;
        }

        public void DeviceDetected()
        {
        }

        public void CallUnityEvent(string s)
        {
            Log.Debug("UnityEvent: " + s);
        }

        public void PluginMessageReceived(string s)
        {
            Log.Debug("PluginMessageReceived: " + s);

            connection.PluginReceived(s);
        }

        public void BoardConnected(string s)
        {
            Log.Debug("USB Ready");
            if (UduinoManager.Instance.autoDiscover)
                UduinoManager.Instance.DiscoverPorts();
        }

        public void BoardDisconnected(string s)
        {
            Log.Debug("USB Disconnected");
            connection.CloseDevices();
        }
    
        public void SendData(byte[] data)
        {
         //   androidPlugin.Call("_SendData", data);
        }


        void OnBleDidInitialize(string message)
        {
            Debug.Log("OnBleDidInitialize" + message);

            if (message == "Success")
            {
                connection.bluetoothStarted = true;
                connection.Discover();
            }
            else
            {
                BluetoothInterface.Instance.DisplayError("Cannot start BLE: " + message);
            }
        }

        void OnBleDidConnect(string message)
        {
            Debug.Log("OnBleDidConnect, TODO : Lancer la detection UduinoIdentity " + message);

            if (message == "Success")
            {
                connection.BoardConnected(); ;
            }
            else 
            {
            }
        }

        void OnBleDidDisconnect(string message)
        {
            Debug.Log("OnBleDidDisconnect" + message);

            if (message == "Success")
            {
                connection.DisconnectedFromSource();
            }
            else 
            {
            }
        }

        void PeripheralScanComplete(string devices)
        {
            var parsedData = JSON.Parse(devices);
            try
            {
                if (parsedData["devices"].Count == 0)
                {
                    // Todo : Do something here
                    Log.Warning("No devices found.");
                    BluetoothInterface.Instance.NoDeviceFound(true);
                }
                else
                {
                    for (int i = 0; i < parsedData["devices"].Count; i++)
                    {
                        connection.DeviceFound(parsedData["devices"][i]["name"], parsedData["devices"][i]["uuid"]);
                        Log.Debug("Device added: " + parsedData["devices"][i]["name"]);
                    }
                }
            } catch(Exception e) {
                if(devices == "NO DEVICE FOUND")
                    Log.Warning("No devices found.");
                else
                    Log.Error("Error when parsing the devices list: " + devices + "\r\n" + e);
            }
            BluetoothInterface.Instance.StopSearching();
        }

    }
}