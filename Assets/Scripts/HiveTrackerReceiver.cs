using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class HiveTrackerReceiver : MonoBehaviour {

    public Vector3 rotationOffset;

    public Transform accelerometer;

    public Transform[] diodes = new Transform[4];

    public bool asignSameRotationToDiodes = true;

    public void SetRotation(float x, float y, float z, float w)
    {
        accelerometer.localRotation = new Quaternion(x, z, y, w) * Quaternion.Euler(rotationOffset);
    }

    public void SetDiode(int diodeID, float x, float y, float z)
    {
        diodes[diodeID].transform.position = new Vector3(x, y, z);

        if (asignSameRotationToDiodes)
            diodes[diodeID].localRotation = accelerometer.localRotation;
    }
}
