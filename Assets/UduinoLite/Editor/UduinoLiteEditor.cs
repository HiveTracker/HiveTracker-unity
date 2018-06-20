using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UduinoLite))]
public class UduinoLiteEditor : Editor
{
    UduinoLite uduinoLite = null;

    public void Awake()
    {
        if (uduinoLite == null)
            uduinoLite = (UduinoLite)target;
    }


    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
      //  EditorGUILayout.LabelField("Faire ici un bouton discvoer ports, qui affiche les ports et ou on peut sélectionner le bon");
    }

    #region FindBoard names

    /*
    public override void FindBoards(UduinoManager manager)
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
	     Discover(GetUnixPortNames());
#else
        Discover(GetWindowsPortNames());
#endif
    }
    private string[] GetWindowsPortNames()
    {
        return SerialPort.GetPortNames();
    }


    private string[] GetUnixPortNames()
    {
        int p = (int)System.Environment.OSVersion.Platform;
        List<string> serial_ports = new List<string>();
        Debug.LogError("Not implemented");
        return serial_ports.ToArray();
    }
    }*/

    #endregion

}
