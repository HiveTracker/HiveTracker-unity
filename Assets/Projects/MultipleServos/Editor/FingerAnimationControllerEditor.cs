using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FingerAnimationController))]
public class FingerAnimationControllerEditor : Editor {

    public override void OnInspectorGUI()
    {
        FingerAnimationController myTarget = (FingerAnimationController)target;

        EditorGUILayout.BeginHorizontal();
        myTarget.debugAnimationName = EditorGUILayout.TextField(myTarget.debugAnimationName);
     
        if (GUILayout.Button("Play Animation"))
        {
            myTarget.PlayAnimation(myTarget.debugAnimationName);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Animation"))
        {
            myTarget.StartAnimation();
        }
        if (GUILayout.Button("Movement finished"))
        {
            myTarget.MovementFinished();
        }
        if (GUILayout.Button("Clear"))
        {
            myTarget.ClearAnimation();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Start"))
        {
            myTarget.StartMotor();
        }
        if (GUILayout.Button("Stop"))
        {
            myTarget.StopMotor();
        }
        if (GUILayout.Button("Cal"))
        {
            myTarget.Calibrate();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        myTarget.globalSpedd = EditorGUILayout.IntField("Speed", myTarget.globalSpedd);
        if (GUILayout.Button("Set speed"))
        {
            myTarget.SetGlobalSpeed();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        DrawDefaultInspector();

    }
}
