using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ValueReceiver : MonoBehaviour {

    public Vector3 offset;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}


    public void Receive(string v)
    {
        /*
        if (v.Length != 37)
        {
            Debug.Log("Loss of data for: \r\n " + v);
            return;
        }
        */

        string[] splitted = v.Split('\t');
        if (splitted.Length != 8)
        {
            Debug.Log("Error when splitting: \r\n " + v);
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

            float z = float.Parse(splitted[splitted.Length - 1]);
            float y = float.Parse(splitted[splitted.Length - 2]);
            float x = float.Parse(splitted[splitted.Length - 3]);

            this.transform.localEulerAngles = new Vector3(z + offset.x, offset.y+ x, offset.z+ y);

        } catch(Exception e)
        {
            Debug.Log(e);
        }
        return;

    }
}
