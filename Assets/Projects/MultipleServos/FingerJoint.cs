using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FingerJoint : MonoBehaviour {

    public Vector3 transformAxis;
    public int offset;
    public int lastReceive;

    void Start () {
		
	}
	
	void Update () {
        if(!Application.isPlaying)
        SetAngle(0);
    }

    public void SetAngle(int angle)
    {
        lastReceive = angle;
        Quaternion targetRotation = Quaternion.Euler(transformAxis * (angle + offset));
        this.transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * 5.0f);
    }
}
