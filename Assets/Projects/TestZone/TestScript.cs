using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Uduino;

public class TestScript : MonoBehaviour {
    public bool toggle = false; public bool disco = false;

    void Awake () {
        UduinoManager.Instance.OnBoardConnected += OnBoardConnected;
        UduinoManager.Instance.OnBoardDisconnected += OnBoardDisconnected;
    }

    void Update () {
        if (UduinoManager.Instance.hasBoardConnected())
        {
            UduinoDevice firstBoard = UduinoManager.Instance.GetBoard("uduinoBoard");
            UduinoDevice secondBoard = UduinoManager.Instance.GetBoard("uduinoBoard2");
            UduinoManager.Instance.digitalWrite(firstBoard, 13, toggle ? State.HIGH : State.LOW);
            UduinoManager.Instance.digitalWrite(secondBoard, 12, !toggle ? State.HIGH : State.LOW);
        }
        if (disco)
        {
            UduinoManager.Instance.DiscoverPorts();
            disco = false;
        }
    }

    void OnBoardConnected(UduinoDevice connectedDevice)
    {
        if (connectedDevice.name == "uduinoBoard")
        {
            UduinoManager.Instance.pinMode(connectedDevice, 13, PinMode.Output);
            UduinoManager.Instance.pinMode(connectedDevice, 13, PinMode.Input_pullup);
        }
        else if (connectedDevice.name == "uduinoBoard2")
        {
            UduinoManager.Instance.pinMode(connectedDevice, 12, PinMode.Output);
        } else
        {
            Debug.Log("The board " + connectedDevice.name + " is connected.");
        }
    }

    void OnBoardDisconnected(UduinoDevice connectedDevice)
    {
      
        Debug.Log("The board " + connectedDevice.name + " is disconnected.");
    }
}