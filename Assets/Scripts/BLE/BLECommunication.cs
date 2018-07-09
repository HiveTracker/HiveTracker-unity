using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;

public class BLEReceiver : MonoBehaviour
{
    public BLEConnection connection = null;
    public static BLEReceiver Instance = null;
    public string id = null;

    public void Awake()
    {
        Instance = this;
    }

    public void DeviceDetected() { }

    public void CallUnityEvent(string s)
    {
        Debug.Log("UnityEvent: " + s);
    }

    public void PluginMessageReceived(string s)
    {
        Debug.Log("PluginMessageReceived: " + s);

        connection.PluginReceived(s);
    }

    public void BoardConnected(string s)
    {
        Debug.Log("USB Ready");
    }

    public void BoardDisconnected(string s)
    {
        Debug.Log("USB Disconnected");
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


    public void PluginWrite(string m) { }

    void PeripheralScanComplete(string devices)
    {
        var parsedData = JSON.Parse(devices);
        try
        {
            if (parsedData["devices"].Count == 0)
            {
                // Todo : Do something here
                Debug.Log("No devices found.");
                BluetoothInterface.Instance.NoDeviceFound(true);
            }
            else
            {
                for (int i = 0; i < parsedData["devices"].Count; i++)
                {
                    connection.DeviceFound(parsedData["devices"][i]["name"], parsedData["devices"][i]["uuid"]);
                    Debug.Log("Device added: " + parsedData["devices"][i]["name"]);
                }
            }
        } catch(Exception e) {
            if(devices == "NO DEVICE FOUND")
                Debug.Log("No devices found.");
            else
                Debug.LogError("Error when parsing the devices list: " + devices + "\r\n" + e);
        }
        BluetoothInterface.Instance.StopSearching();
    }

}