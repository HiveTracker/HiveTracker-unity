using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Uduino;

public class AdvancedButton : MonoBehaviour {

    void Awake()
    {
        UduinoManager.Instance.OnDataReceived += OnDataReceived; //Create the Delegate
        UduinoManager.Instance.alwaysRead = true; // This value should be On By Default
    }



    private void Update()
    {
        UduinoManager.Instance.sendCommand("maCommand");    
    }


    void OnDataReceived(string data, UduinoDevice deviceName)
    {
        if (data == "1")
            Debug.Log("Data");
        else if (data == "0")
            Debug.Log("Data");
    }
}
