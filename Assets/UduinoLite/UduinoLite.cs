using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
//#if UDUINOLITE_READY
using System.IO.Ports;
//#endif

public class UduinoLite : MonoBehaviour {

    #region Instance
    public static UduinoLite Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            UduinoLite[] uduinoManagers = FindObjectsOfType(typeof(UduinoLite)) as UduinoLite[];
            if (uduinoManagers.Length == 0)
            {
                Debug.Log("UduinoLite not present on the scene. Creating a new one.");
                UduinoLite manager = new GameObject("UduinoLite").AddComponent<UduinoLite>();
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
                Debug.Log("You can only use one UduinoLite. Destroying the new one attached to the GameObject " + value.gameObject.name);
                Destroy(value);
            }
        }
    }
    private static UduinoLite _instance = null;
    #endregion
    
    [Header("UduinoLite")]
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
    public ValueReceived ValueReceivedEvent;

    private void Start()
    {
        if (OpenOnStart)
            OpenPort();
    }

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


#region Thread
    Action callbackAction = null;
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

            if (callbackAction == null)
            {
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
            }
            else
            {
                callbackAction();
                callbackAction = null;
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
                string readedLine = serial.ReadLine();
                lock(readQueue)
                    readQueue.Enqueue(readedLine);
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

    void WriteArduino()
    {

    }

    private void OnApplicationPause(bool pause)
    {
        if(pause)
        Close();
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
                Debug.Log("CLosed");
            }
            catch(Exception e)
            {
                Debug.Log(e);
            }

        }
    }

    void Awake()
    {
        ChildThread = new Thread(ChildThreadLoop);
        ChildThread.Start();
    }


    void Update()
    {
        MainThreadWait.WaitOne();
        MainThreadWait.Reset();

        //  Debug.Log("Do stuff to A");
        //callbackAction = () => Debug.Log("Do stuff");

        // Copy Results out of the thread
        // Copy pending changes into the thread
        lock (readQueue)
        {
            if (readQueue.Count > 0)
                ValueReceivedEvent.Invoke(readQueue.Dequeue());
        }


        ChildThreadWait.Set();
    }

    #endregion


    #region List port
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
            Debug.Log(portList);
    }
    #endregion


}
