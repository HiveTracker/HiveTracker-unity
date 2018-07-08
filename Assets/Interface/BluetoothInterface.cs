using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



public enum AndroidInterface
{
    Minimal,
    Full,
    None
}

public class BLEDeviceButton_Interface
    {
        public Button button;
        public GameObject connecting;
        public GameObject connect;
        public Transform connected;
        public GameObject disconnect;

        public BLEDeviceButton_Interface(Button button)
        {
            this.button = button;
            Transform mainT = button.transform;
            this.connect = mainT.Find("Connect").gameObject;
            this.connecting = mainT.Find("Connecting").gameObject;
            this.connected = mainT.Find("Connected");
            this.disconnect = mainT.Find("Disconnect").gameObject;

            CanConnect();
        }

        public void CanConnect()
        {
            connect.SetActive(true);
            disconnect.SetActive(false);
            connecting.SetActive(false);
            connected.gameObject.SetActive(false);
            button.enabled = true;
        }

        public void Connecting()
        {
            connect.SetActive(false);
            connecting.SetActive(true);
            button.enabled = false;
            disconnect.SetActive(true);
        }

        public void Connected()
        {
            connecting.SetActive(false);
            connected.gameObject.SetActive(true);
        }

        public void Disconnected()
        {
            CanConnect();
        }
    }

    public class BluetoothInterface : MonoBehaviour
    {
        #region singleton
        public static BluetoothInterface Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                BluetoothInterface[] uduinoManagers = FindObjectsOfType(typeof(BluetoothInterface)) as BluetoothInterface[];
                if (uduinoManagers.Length == 0)
                {
                    Debug.Log("UduinoManager not present on the scene. Creating a new one.");
                    BluetoothInterface manager = new GameObject("BluetoothInterface").AddComponent<BluetoothInterface>();
                    _instance = manager;
                    return _instance;
                }
                else
                {
                    return uduinoManagers[0];
                }
            }
            set
            {
                if (_instance == null)
                    _instance = value;
                else
                {
                Debug.Log("You can only use one UduinoManager. Destroying the new one attached to the GameObject " + value.gameObject.name);
                    Destroy(value);
                }
            }
        }
        private static BluetoothInterface _instance = null;
    #endregion

        BLEConnection bleCommunication = null;
        public Dictionary<string, BLEDeviceButton_Interface> devicesButtons = new Dictionary<string, BLEDeviceButton_Interface>();

        [Header("Full panel")]
        public GameObject fullUI;
        public GameObject scanButtonFull;
        public GameObject fullDevicePanel;
        public GameObject errorPanel;
        public GameObject notFound;
        public GameObject deviceButton;

        [Header("Minimal Panel")]
        public GameObject minimalUI;
        public GameObject scanButtonMinimal;
        public GameObject minimalDevicePanel;
        public GameObject minimalErrorPanel;
        public GameObject minimalNotFound;
        public GameObject deviceButtonMinimal;

        [Header("Debug Panel")]
        public GameObject debugPanel;
        public Text sendValue;
        public Text lastReceivedValue;
        public AndroidInterface interfaceType = AndroidInterface.Full; // Full, Minimal, None


    void Awake()
        {
            Instance = this;
            switch(interfaceType)
            {
                case AndroidInterface.Full:
                    minimalUI.SetActive(false);
                    fullUI.SetActive(true);
                    break;
                case AndroidInterface.Minimal:
                    minimalUI.SetActive(true);
                    fullUI.SetActive(false);
                break;
                case AndroidInterface.None:
                    minimalUI.SetActive(false);
                    fullUI.SetActive(false);
                    break;
            }
            StopTimer();
            ClearPanel();
            DisplayDebugPanel(false);
        }

        public void Read()
        {

        }

        public void SendValue()
        {
            bleCommunication.PluginWrite(sendValue.text);
        }

        public void LastReceviedValue(string value)
        {
            lastReceivedValue.text = value;
        }

        public void SetBLEConnection(BLEConnection connection)
        {
            bleCommunication = connection;
        }

        #region Start / Stop searching
        public void SearchDevices()
        {
            bleCommunication.ScanForDevices();
        }

        public void StartSearching()
        {
            ClearPanel();
            StartTimer();
            DisplayDebugPanel(false);
            NoDeviceFound(false);
            getScanButton().text = "Scanning...";
            devicesButtons.Clear();
        }

        public void StopSearching()
        {
            getScanButton().text = "Scan for devices";
            getScanSlider().value = 0;
            getScanSlider().gameObject.SetActive(false);
        }

        void StartTimer()
        {
            StartCoroutine(StartSliderCountdown());
        }

        public IEnumerator StartSliderCountdown()
        {
            Slider slider = getScanSlider();
            slider.gameObject.SetActive(true);

            int currentCount = 0 ;
            while (currentCount < bleCommunication.scanDuration * 100)
            {
                yield return new WaitForSeconds(0.01f);
                slider.value = (float)((float)currentCount / (float)(bleCommunication.scanDuration * 100));
                currentCount++;
            }
            StopTimer();
        }

        public void SendCommand(string t)
        {
            bleCommunication.PluginWrite(t + "\r\n");
        }

        void StopTimer()
        {
            getScanSlider().value = 0;
            getScanSlider().gameObject.SetActive(false);
        }

        void ClearPanel()
        {
            foreach (Transform child in getPanel())
                if(child.gameObject.name != "NotFound")
                    Destroy(child.gameObject);

            getErrorPanel().SetActive(false);
        }

        #endregion

        #region Getting elements from differents UIs
        Text getScanButton()
        {
            return interfaceType == AndroidInterface.Full ?
                    scanButtonFull.transform.Find("ScanText").GetComponent<Text>() :
                    scanButtonMinimal.transform.Find("ScanText").GetComponent<Text>();
        }

        Slider getScanSlider()
        {
            return interfaceType == AndroidInterface.Full ?
                    scanButtonFull.transform.Find("Slider").GetComponent<Slider>() :
                    scanButtonMinimal.transform.Find("Slider").GetComponent<Slider>();
        }

        Transform getPanel()
        {
            return interfaceType == AndroidInterface.Full ?
                    fullDevicePanel.transform :
                    minimalDevicePanel.transform;
        }
        GameObject getDeviceButtonPrefab()
        {
            return interfaceType == AndroidInterface.Full ? deviceButton : deviceButtonMinimal;
        }
        GameObject getErrorPanel()
        {
            return interfaceType == AndroidInterface.Full ? errorPanel : minimalErrorPanel;
        }

        GameObject getNotFound()
        {
            return interfaceType == AndroidInterface.Full ? notFound : minimalNotFound;
        }

        #endregion 

        public void AddDevicesButtons(string name, string uuid)
        {
            if (interfaceType == AndroidInterface.None)
                return;

            GameObject deviceBtn = GameObject.Instantiate(getDeviceButtonPrefab(), getPanel());
            deviceBtn.transform.name = uuid;
            deviceBtn.transform.Find("DeviceName").transform.GetComponent<Text>().text = name;
            Button btn = deviceBtn.GetComponent<Button>();

            BLEDeviceButton_Interface deviceInterface = new BLEDeviceButton_Interface(btn);
            devicesButtons.Add(name, deviceInterface);

            // Add connect event
            btn.onClick.AddListener(() => bleCommunication.ConnectPeripheral(uuid, name));

            // Add disconnect event
            deviceInterface.disconnect.GetComponent<Button>().onClick.AddListener(() => this.DisconnectDevice());
        }

        public void NoDeviceFound(bool active)
        {
            getNotFound().SetActive(active);
        }

        public void DisplayError(string message)
        {
            if(message == "")
            {
                getErrorPanel().SetActive(false);
            }
            else
            {
                getErrorPanel().SetActive(true);
                getErrorPanel().transform.Find("Content").Find("ErrorMessage").Find("ErrorText").GetComponent<Text>().text = message;
            }
        }

        public void DisplayDebugPanel(bool active)
        {
            debugPanel.SetActive(active);
        }

        public void UduinoConnecting(string name)
        {
            BLEDeviceButton_Interface currentDeviceBtn = null;
            if (devicesButtons.TryGetValue(name, out currentDeviceBtn))
            {
                currentDeviceBtn.Connecting();
            }
            Debug.Log("connecting to " + name);
        }

        public void UduinoConnected(string name)
        {
            BLEDeviceButton_Interface currentDeviceBtn = null;
            if(devicesButtons.TryGetValue(name, out currentDeviceBtn)) {
                DisplayDebugPanel(true);
                currentDeviceBtn.Connected();
            }
        }

        public void UduinoDisconnected(string name)
        {
            BLEDeviceButton_Interface currentDeviceBtn = null;
            if (devicesButtons.TryGetValue(name, out currentDeviceBtn))
            {
                DisplayDebugPanel(false);
                currentDeviceBtn.Disconnected();
            }
        }

        public void DisconnectDevice()
        {
            bleCommunication.Disconnect();
        }
    }