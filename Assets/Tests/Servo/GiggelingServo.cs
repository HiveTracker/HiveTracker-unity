using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Uduino;

public class GiggelingServo : MonoBehaviour {


        UduinoManager u; // The instance of Uduino is initialized here
        public int servoPin = 6;
        [Range(0, 180)]
        public int angle = 0;
        public int angleRot = 1;

        void Start()
        {
            UduinoManager.Instance.pinMode(servoPin, PinMode.Servo);
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            while (true)
            {
                UduinoManager.Instance.analogWrite(servoPin, angle);
                angle += angleRot;
                if (angle <= 0 || angle >= 255) angleRot = -angleRot;
                yield return new WaitForSeconds(0.1f);
            }
        }
}
