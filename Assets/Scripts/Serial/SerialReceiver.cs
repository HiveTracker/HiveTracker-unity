using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.IO.Ports;

public class SerialReceiver : MonoBehaviour {

    #region Instance
    public static SerialReceiver Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            SerialReceiver[] serialManagers = FindObjectsOfType(typeof(SerialReceiver)) as SerialReceiver[];
            if (serialManagers.Length == 0)
            {
                Debug.Log("SerialReceiver not present on the scene. Creating a new one.");
                SerialReceiver manager = new GameObject("serialManagers").AddComponent<SerialReceiver>();
                _instance = manager;
                return _instance;
            }
            else
            {
                return serialManagers[0];
            }
        }
        set
        {
            if (_instance == null)
                _instance = value;
            else
            {
                Debug.Log("You can only use one SerialReceiver. Destroying the new one attached to the GameObject " + value.gameObject.name);
                Destroy(value);
            }
        }
    }
    private static SerialReceiver _instance = null;
    #endregion

    public HiveTrackerReceiver hiveTracker;

    [Header("SerialReceiver")]
    public bool OpenOnStart = true;

    [Header("Serial Settings")]
    [Tooltip("Right click to detect ports")]
    [ContextMenuItem("Detect Port", "ListPort")]
    public string portName = "COM4";
    public int baudRate = 115200;
    [Range(10,500)]
    public int readTimeout = 10;
    [Range(10, 500)]
    public int writeTimeout = 10;
    public SerialPort serial = null;

    public enum SerialCapabilities
    {
        Read,
        Write,
        ReadWrite
    }
    public SerialCapabilities serialCapabilities = SerialCapabilities.Read;
    
    public enum PortState
    {
        Close,
        Open
    }
    [Space]
    public PortState boardState = PortState.Close;


    [System.Serializable]
    public class ValueReceived : UnityEvent<string> {}
    [Header("Events")]
    public bool useReceiveEvents = false;
    public ValueReceived ValueReceivedEvent;

    public bool startSerial = false;
    private void Start()
    {
        if (OpenOnStart && startSerial)
            OpenPort();
    }



    void Awake()
    {
        if (startSerial)
        {
            ChildThread = new Thread(ChildThreadLoop);
            ChildThread.Start();
        } else
        {
            this.enabled = false;
        }
    }


    void Update()
    {
        MainThreadWait.WaitOne();
        MainThreadWait.Reset();


        if(mainThreadCallback != null)
        {
            try
            {
                mainThreadCallback();
            } catch (Exception e) {
                Debug.Log(e);
            }
            mainThreadCallback = null;
        }



        if (useReceiveEvents)
        {
            lock (readQueue)
            {
                if (readQueue.Count > 0)
                    ValueReceivedEvent.Invoke(readQueue.Dequeue());
            }
        }
        
        ChildThreadWait.Set();
    }


    #region Open Serial
    void OpenPort()
    {
        try
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            portName = "\\\\.\\" + portName;
#endif
            serial = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            serial.ReadTimeout = readTimeout;
            serial.WriteTimeout = 10;
            serial.Close();
            serial.Dispose();
            serial.Open();
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
            serial.BaseStream.Flush();
            serial.NewLine = "\n";
            boardState = PortState.Open;
            Debug.Log("The port is open");
        }
        catch (Exception e)
        {
            Debug.Log("Error" + e);
        }

    }

    #endregion


    #region Closing Serial port
    private void OnApplicationPause(bool pause)
    {
       // if (pause)
       //     Close();
    }

    private void OnApplicationQuit()
    {
        Close();
    }

    private void OnDisable()
    {
        Close();
    }

    void Close()
    {
        Debug.Log("Stopping Thread");

        ChildThreadWait.Close();
        MainThreadWait.Close();
        try
        {
            ChildThread.Abort();
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }

        if (serial != null)
        {
            try
            {
                serial.Close();
                serial = null;
                Debug.Log("Serial closed.");
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }

        }
    }
    #endregion

    #region Serial Reading
    Action mainThreadCallback = null;
    Thread ChildThread = null;
    EventWaitHandle ChildThreadWait = new EventWaitHandle(true, EventResetMode.ManualReset);
    EventWaitHandle MainThreadWait = new EventWaitHandle(true, EventResetMode.ManualReset);

    Queue<string> readQueue = new Queue<string>();

    void ChildThreadLoop()
    {
        ChildThreadWait.Reset();
        ChildThreadWait.WaitOne();

        while (true)
        {
            ChildThreadWait.Reset();

            switch(serialCapabilities)
            {
                case SerialCapabilities.Read:
                    ReadArduino();
                    break;
                case SerialCapabilities.Write:
                    WriteArduino();
                    break;
                case SerialCapabilities.ReadWrite:
                    ReadArduino();
                    WriteArduino();
                    break;
            }
            
           // Debug.Log("wait for end of frame");
            WaitHandle.SignalAndWait(MainThreadWait, ChildThreadWait);
        }
    }

    void ReadArduino()
    {
        try
        {
            try
            {
                #region remove
                /*
                int lineCount = 0;
                string tempBuffer = "";
                for (int i=0; i <150;i++)
                {
                    char a = (char)serial.ReadByte();
                    tempBuffer += a.ToString();
                  //  Debug.Log(a);
                    if (tempBuffer.EndsWith("\n"))
                    {
                        Debug.Log(tempBuffer);
                        if (lineCount > 5)
                        {
                            serial.DiscardOutBuffer();
                            serial.DiscardInBuffer();
                        }
                    }
                }
                */
                #endregion
                string readedLine = serial.ReadLine();
                if(useReceiveEvents) lock (readQueue) readQueue.Enqueue(readedLine);
                
                ProcessData(readedLine);
            }
            catch (TimeoutException e)
            {
                Debug.Log(e);
            }
        } catch(Exception e)
        {
            Debug.Log(e);
        }
    }


    void ProcessData(string inData)
    {
        /*
              if (v.Length != 37)
              {
                  Debug.Log("Loss of data for: \r\n " + v);
                  return;
              }
              */

        string[] splitted = inData.Split('\t');
        if (splitted.Length != 4)
        {
            Debug.Log("Error when splitting: \r\n " + inData);
            return;
        }
        /*
                for(int i = 0;i < splitted.Length;i++)
                {
                    Debug.Log(splitted[i]);
                }
                */
        try
        {

            float x = float.Parse(splitted[splitted.Length - 1]);
            float y = float.Parse(splitted[splitted.Length - 2]);
            float z = float.Parse(splitted[splitted.Length - 3]);
            float w = float.Parse(splitted[splitted.Length - 4]);

            mainThreadCallback = () =>
            {
                hiveTracker.SetRotation(x, y, z, w);
            };

        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        return;
    }
    void WriteArduino() { }


    #endregion


    #region List port on right click
    [ContextMenu("List available serial ports")]
    private void ListPort()
    {
        string[] portList = SerialPort.GetPortNames();
        if (portList.Length == 0)
        {
            Debug.Log("No ports are connected on your computer");
            return;
        }
        Debug.Log("Port available in the computer:");
        foreach (string s in portList)
            Debug.Log(s);
    }
    #endregion


}
