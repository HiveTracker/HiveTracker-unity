using System.Collections.Generic;
using UnityEngine;

namespace Uduino
{
    public class UduinoConnection_AndroidBluetoothLE : UduinoConnection
    {
        AndroidJavaClass _class = null;
        AndroidJavaObject androidPlugin { get { return _class.GetStatic<AndroidJavaObject>("instance"); } }
        UduinoCommunication_AndroidBluetoothLE communicationController = null;

        string javaClassName = "com.mteys.uduinobluetooth.AndroidBluetooth";
        public bool bluetoothStarted = false;

        Dictionary<string, string> availableDevices = new Dictionary<string, string>();

        public UduinoConnection_AndroidBluetoothLE() : base() { }

        public override void FindBoards(UduinoManager manager)
        {
            base.FindBoards(manager); // Add reference to manager
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
        public override void Discover()
        {
            if (UduinoCommunication_AndroidBluetoothLE.Instance == null)
            {
                communicationController = CreateCommunicationController();
                InitCommunication(communicationController.id);
                Debug.Log("Create new communication controller");
            }
            if (bluetoothStarted)
                ScanForDevices();
        }



        public void ScanForDevices()
        {
            Debug.Log("Scan for devices");
            androidPlugin.Call("_ScanForPeripherals", UduinoManager.Instance.bleScanDuration * 1000);
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
            if (connectedDevice == null)
            {
                result = androidPlugin.Call<bool>("_ConnectPeripheral", peripheralID);
                BluetoothInterface.Instance.UduinoConnecting(name);
            } else
            {
                Log.Debug("A board is already trying to be connected");
            }
            return result;
        }

        public void BoardConnected()
        {
            connectedDevice = OpenUduinoDevice("");
            connectedDevice.Open();
            connectedDevice.OnBoardClosed += DisconnectedFromSource;
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
            if (connectedDevice != null)
            {
                connectedDevice.OnBoardClosed -= DisconnectedFromSource;
                BluetoothInterface.Instance.UduinoDisconnected(connectedDevice.name);
                UduinoManager.Instance.CloseDevice(connectedDevice);
                connectedDevice = null;
            }
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

        UduinoCommunication_AndroidBluetoothLE CreateCommunicationController()
        {
            string randName = "AndroidCommunication" + Random.Range(0, 100);
            UduinoCommunication_AndroidBluetoothLE communication = new GameObject(randName).AddComponent<UduinoCommunication_AndroidBluetoothLE>();
            communication.connection = this;
            communication.id = randName;
            return communication;
        }

        public override UduinoDevice OpenUduinoDevice(string id)
        {
            return new UduinoDevice_AndroidBluetoothLE(this);
        }

        public override void PluginWrite(string message)
        {
            androidPlugin.Call("_SendData", message);
            Debug.Log("_SendData : " + message);
          //  Log.Info("<color=#4CAF50>" + message + "</color> sent to <color=#2196F3>[" + connectedDevice.name + "]</color>");
        }

        public override void PluginReceived(string message)
        {
            if (connectedDevice != null)
            {
                BluetoothInterface.Instance.LastReceviedValue(message);
                connectedDevice.AddToArduinoReadQueue(message);
                if (connectedDevice.boardStatus == BoardStatus.Open)
                {
                    DetectUduino(connectedDevice);                }
                else if (connectedDevice.boardStatus == BoardStatus.Found || connectedDevice.boardStatus == BoardStatus.Finding)
                {
                    connectedDevice.MessageReceived(message);
                }
                else
                {
                    //TODO : What ?
                }
            }
        }

    }
}