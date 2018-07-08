using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BLEReceiver : MonoBehaviour {
    public bool useBLE = true;

    BLEConnection connection;

    // Use this for initialization
    void Start () {
        if (useBLE)
        {
            connection = new BLEConnection();
            connection.FindBoards();
        }

    }

    // Update is called once per frame
    void Update () {
		
	}
}
