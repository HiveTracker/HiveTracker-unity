using System.Collections.Generic;
using UnityEngine;


public class BLEConnection
{
    AndroidJavaClass _class = null;
    AndroidJavaObject androidPlugin { get { return _class.GetStatic<AndroidJavaObject>("instance"); } }
    BLECommunication communicationController = null;

    string javaClassName = "com.hivetracker.ble.AndroidBluetooth";
    public bool bluetoothStarted = false;

    Dictionary<string, string> availableDevices = new Dictionary<string, string>();

    public void CreateDebugCanvas()
    {
        GameObject debugCanvas = new GameObject();
        debugCanvas.AddComponent<Uduino.UduinoDebugCanvas>();
    }

    public void FindBoards()
    {
        CreateDebugCanvas();
        Discover();
        BluetoothInterface.Instance.SetBLEConnection(this);
    }

    void InitCommunication(string communicationGoName)
    {
        _class = new AndroidJavaClass(javaClassName); //The arduino baord should be found automatically
        _class.CallStatic("start", communicationGoName);
        Debug.Log("Starting service with name " + communicationGoName);
        //Debug.Log("Setting baud rate to:" + _manager.BaudRate);
        //androidPlugin.Call("ChangeBaudRate", _manager.BaudRate);
    }

    #region Scanning
    public void Discover()
    {
        if (BLECommunication.Instance == null)
        {
            communicationController = CreateCommunicationController();
            InitCommunication(communicationController.id);
            Debug.Log("Create new communication controller");
        }
        if (bluetoothStarted)
            ScanForDevices();
    }

    public float scanDuration = 15;


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

    public void List()
    {
        GetListOfDevices();
    }

    public bool SearchDevicesDidFinish()
    {
        bool searchDevicesDidFinish = false;
        searchDevicesDidFinish = androidPlugin.Call<bool>("_SearchDeviceDidFinish");
        return searchDevicesDidFinish;
    }

    public string GetListOfDevices()
    {
        string listOfDevices = "";
        listOfDevices = androidPlugin.Call<string>("_GetListOfDevices");
        Debug.Log("listOfDevices" + listOfDevices);
        return listOfDevices;
    }
    #endregion

    #region Device connection 
    public bool IsDeviceConnected()
    {
        bool isConnected = false;
        isConnected = androidPlugin.Call<bool>("_IsDeviceConnected");
        return isConnected;
    }

    public bool ConnectPeripheral(string peripheralID, string name)
    {
        bool result = false;
        result = androidPlugin.Call<bool>("_ConnectPeripheral", peripheralID);
        BluetoothInterface.Instance.UduinoConnecting(name);
        return result;
    }

    public void BoardConnected()
    {
        Debug.Log("Board connecged");
     //   OpenUduinoDevice("");
    }
    #endregion

    #region Disconnect
    public bool Disconnect()
    {
        if(androidPlugin.Call<bool>("_Disconnect"))
        {
            DisconnectedFromSource();
        }
        return true;
    }

    public void DisconnectedFromSource()
    {
            BluetoothInterface.Instance.UduinoDisconnected("name");
    }
    #endregion

    #region Communication 
    public string GetData()
    {
        string result = null;
        result = androidPlugin.Call<string>("_GetData");
        return result;
    }
    #endregion

    BLECommunication CreateCommunicationController()
    {
        string randName = "AndroidCommunication" + Random.Range(0, 100);
        BLECommunication communication = new GameObject(randName).AddComponent<BLECommunication>();
        communication.connection = this;
        communication.id = randName;
        return communication;
    }

    public void PluginWrite(string message)
    {
        androidPlugin.Call("_SendData", message);
        Debug.Log("_SendData : " + message);
        //  Log.Info("<color=#4CAF50>" + message + "</color> sent to <color=#2196F3>[" + connectedDevice.name + "]</color>");
    }

    public void PluginReceived(string message)
    {

        BluetoothInterface.Instance.LastReceviedValue(message);
        Debug.Log(message);
    }

}