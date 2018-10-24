using System.Collections.Generic;
using UnityEngine;


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


    [Header("Debug settings")]
    //todo : use that;
    public bool deviceConnected = false;
    BLEReceiver communicationController = null;
    public BluetoothState bluetoothState = BluetoothState.Disabled;

    // Android BLE
    AndroidJavaClass _class = null;
    AndroidJavaObject androidPlugin { get { return _class.GetStatic<AndroidJavaObject>("instance"); } }
    string javaClassName = "com.hivetracker.ble.AndroidBluetooth";


    // Windows BLE


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
    }
    #endregion

    #region Disconnect
    public bool Disconnect()
    {
        Debug.Log("CLick on disconnect");
        if(androidPlugin.Call<bool>("_Disconnect"))
        {
            DisconnectedFromSource();
        }
        return true;
    }

    public void DisconnectedFromSource()
    {
        BluetoothInterface.Instance.BLEDisconected("UART");
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