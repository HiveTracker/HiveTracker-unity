using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;


public enum ConnectionType
{
    None,
    Windows,
    Android
}
public enum BluetoothState
{
    Disabled,
    Running,
    Connected
}

public class BLEConnection : MonoBehaviour
{
    [Header("Bluetooth settings")]
    Dictionary<string, string> availableDevices = new Dictionary<string, string>();
    public ConnectionType connectionType = ConnectionType.Windows;
    public float scanDuration = 5;
    public string boardConnectedName;

    [Header("Debug settings")]
    //todo : use that;
    public bool deviceConnected = false;
    BLEReceiver communicationController = null;
    public BluetoothState bluetoothState = BluetoothState.Disabled;

    #region Android
    // Android BLE
    AndroidJavaClass _class = null;
    AndroidJavaObject androidPlugin { get { return _class.GetStatic<AndroidJavaObject>("instance"); } }
    string javaClassName = "com.hivetracker.ble.AndroidBluetooth";
    #endregion

    #region Windows
    // Windows BLE
    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEConnectCallbacks(
    [MarshalAs(UnmanagedType.FunctionPtr)]SendBluetoothMessageDelegate sendMessage,
    [MarshalAs(UnmanagedType.FunctionPtr)]DebugDelegate log,
    [MarshalAs(UnmanagedType.FunctionPtr)]DebugDelegate warning,
    [MarshalAs(UnmanagedType.FunctionPtr)]DebugDelegate error);
    public delegate void DebugDelegate([MarshalAs(UnmanagedType.LPStr)]string message);
    public delegate void SendBluetoothMessageDelegate([MarshalAs(UnmanagedType.LPStr)]string gameObjectName, [MarshalAs(UnmanagedType.LPStr)]string methodName, [MarshalAs(UnmanagedType.LPStr)]string message);

    private static DebugDelegate _LogDelegate;
    private static DebugDelegate _WarningDelegate;
    private static DebugDelegate _ErrorDelegate;
    private static SendBluetoothMessageDelegate _SendMessageDelegate;

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLELog([MarshalAs(UnmanagedType.LPStr)]string message);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEInitialize(bool asCentral, bool asPeripheral, string goName);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEDeInitialize();

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEPauseMessages(bool isPaused);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEScanForPeripheralsWithServices([MarshalAs(UnmanagedType.LPStr)]string serviceUUIDsString, bool allowDuplicates, bool rssiOnly, bool clearPeripheralList);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLERetrieveListOfPeripheralsWithServices([MarshalAs(UnmanagedType.LPStr)]string serviceUUIDsString);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEStopScan();

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEConnectToPeripheral([MarshalAs(UnmanagedType.LPStr)]string name);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEDisconnectPeripheral([MarshalAs(UnmanagedType.LPStr)]string name);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEReadCharacteristic([MarshalAs(UnmanagedType.LPStr)]string name, [MarshalAs(UnmanagedType.LPStr)]string service, [MarshalAs(UnmanagedType.LPStr)]string characteristic);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEWriteCharacteristic([MarshalAs(UnmanagedType.LPStr)]string name, [MarshalAs(UnmanagedType.LPStr)]string service, [MarshalAs(UnmanagedType.LPStr)]string characteristic, byte[] data, int length, bool withResponse);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLESubscribeCharacteristic([MarshalAs(UnmanagedType.LPStr)]string name, [MarshalAs(UnmanagedType.LPStr)]string service, [MarshalAs(UnmanagedType.LPStr)]string characteristic);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEUnSubscribeCharacteristic([MarshalAs(UnmanagedType.LPStr)]string name, [MarshalAs(UnmanagedType.LPStr)]string service, [MarshalAs(UnmanagedType.LPStr)]string characteristic);

    [DllImport("HiveWinBLE")]
    private static extern void _winBluetoothLEDisconnectAll();

    [DllImport("HiveWinBLE")]
    private static extern void _threadLoop();

    string serviceGUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
    string subscribeCharacteristic = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E";
    string writeCharacteristic = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

    static readonly Queue<Action> incommingMessages = new Queue<Action>();
    #endregion

    #region Initialization
    public void Start()
    {

        if (connectionType != ConnectionType.None)
        {
            if(connectionType == ConnectionType.Android)
                CreateDebugCanvas();

            Discover(); // TODO ça devrai etre enlevé d'ici ?
            BluetoothInterface.Instance.SetBLEConnection(this);
        }
    }

    void InitCommunication(string communicationGoName)
    {
        if (connectionType == ConnectionType.Android)
        {
            _class = new AndroidJavaClass(javaClassName); //The arduino baord should be found automatically
            _class.CallStatic("start", communicationGoName);
            Debug.Log("Starting Android BLEService with name " + communicationGoName);
        }
        else if (connectionType == ConnectionType.Windows)
        {
        }
    }

    BLEReceiver CreateCommunicationController()
    {
        string randName = "BLEReceiver";
        BLEReceiver communication = new GameObject(randName).AddComponent<BLEReceiver>();
        communication.connection = this;
        communication.id = randName;
        return communication;
    }
    #endregion

    #region Debug
    public void CreateDebugCanvas()
    {
        GameObject debugCanvas = new GameObject();
        debugCanvas.AddComponent<AndroidDebugCanvas>();
    }

    // To remove
    public string GetListOfDevices()
    {
        string listOfDevices = "";
        listOfDevices = androidPlugin.Call<string>("_GetListOfDevices");
        Debug.Log("listOfDevices" + listOfDevices);
        return listOfDevices;
    }
    #endregion

    #region Scanning
    public void Discover()
    {
        if (BLEReceiver.Instance == null)
        {
            communicationController = CreateCommunicationController();
            InitCommunication(communicationController.id);
            Debug.Log("Create new communication controller");
        }
        if (bluetoothState!=BluetoothState.Disabled)
            ScanForDevices();
    }

    public void ScanForDevices()
    {
        Debug.Log("Scan for devices");
        androidPlugin.Call("_ScanForPeripherals", (int)(scanDuration * 1000));
        BluetoothInterface.Instance.StartSearching();
    }

    public void DeviceFound(string name, string uuid)
    {
        availableDevices.Clear();

        if (!availableDevices.ContainsKey(name))
        {
            availableDevices.Add(name, uuid);
            BluetoothInterface.Instance.AddDevicesButtons(name, uuid);
        }
    }

    public bool SearchDevicesDidFinish()
    {
        bool searchDevicesDidFinish = false;
        searchDevicesDidFinish = androidPlugin.Call<bool>("_SearchDeviceDidFinish");
        return searchDevicesDidFinish;
    }
    #endregion

    #region Device connection 
    public bool IsDeviceConnected()
    {
        bool isConnected = false;
        if (connectionType == ConnectionType.Android)
            isConnected = androidPlugin.Call<bool>("_IsDeviceConnected");
        else
            Debug.Log("Not implemented");

        return isConnected;
    }

    public bool ConnectPeripheral(string peripheralID, string name)
    {
        bool result = false;
        if (connectionType == ConnectionType.Android)
        {
            result = androidPlugin.Call<bool>("_ConnectPeripheral", peripheralID);
        }
        else if (connectionType == ConnectionType.Windows)
        {
            _winBluetoothLEConnectToPeripheral(peripheralID);
            result = true;
        }
        BluetoothInterface.Instance.UduinoConnecting(name);
        return result;
    }

    public void BoardConnected()
    {
        Debug.Log("Board connected");
    }
    #endregion

    #region Disconnect
    public bool Disconnect()
    {
        Debug.Log("CLick on disconnect");

        if (connectionType == ConnectionType.Android)
        {
            if (androidPlugin.Call<bool>("_Disconnect")) {
                DisconnectedFromSource();
            }
        }
        else if (connectionType == ConnectionType.Windows)
        {
            UnSubscribeRead();
            Debug.Log("TODO");
            //_winBluetoothLEDisconnectPeripheral(connection.connectedDevice.identity);
        }


        return true;
    }

    public void DisconnectedFromSource()
    {
        BluetoothInterface.Instance.BLEDisconected("UART"); // Todo : the name is hard coded here
    }
    #endregion


    #region WIndows specific
    void SubscribeRead()
    {
        Debug.Log("TODO");
//        _winBluetoothLESubscribeCharacteristic(connection.connectedDevice.identity, serviceGUID, writeCharacteristic);
    }

    void UnSubscribeRead()
    {
        Debug.Log("<color=#ff0000>unsubscriberead</color>");
        Debug.Log("TODO");
//        _winBluetoothLEUnSubscribeCharacteristic(connection.connectedDevice.identity, serviceGUID, writeCharacteristic);
    }
    #endregion

    #region Communication 
    public string GetData()
    {
        string result = null;
        result = androidPlugin.Call<string>("_GetData");
        return result;
    }

    public void PluginWrite(string message)
    {
        androidPlugin.Call("_SendData", message);
        Debug.Log("_SendData : " + message);
    }

    public void PluginReceived(string message)
    {
        BluetoothInterface.Instance.LastReceviedValue(message);
        Debug.Log(message);
    }
    #endregion

}