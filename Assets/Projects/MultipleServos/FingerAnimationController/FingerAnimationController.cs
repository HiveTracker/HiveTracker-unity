using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Uduino;


public enum FingerAnimationType
{
    Once, 
    Loop, 
    Ping_pong
}

public enum FingerAnimationState
{
    Init,
    Moving,
    Finished
}

[System.Serializable]
public class FingerAnimationPoint
{
    public int distalAngle;
    public int midalAngle;
    public int proximalAngle;
    public int dorsalAngle;
    public int phonelAngle;

    public void SendAngleToArduino()
    {
        string message = UduinoManager.BuildMessageParameters(distalAngle, midalAngle, proximalAngle, dorsalAngle, phonelAngle);
        UduinoManager.Instance.sendCommand("move", message);
    }
}

[System.Serializable]
public class FingerAnimation
{
    public string animationName;

    public FingerAnimationType animationType = FingerAnimationType.Once;
    public int numberOfRepetition = 1;
    public FingerAnimationPoint[] animationPoints = new FingerAnimationPoint[3];

    [HideInInspector]
    public int currentStep = -1;
    int direction = 1;

    int currentRepetinion = 1;

    public void Init(int numberOfRepet = -1)
    {
        currentStep = -1;
        direction = 1;
        currentRepetinion = 0;
        if (numberOfRepet != -1)
            numberOfRepetition = numberOfRepet;

        Debug.Log("Starting animation " + animationName + " repeated " + numberOfRepetition + " times");
    }

    public void Stop()
    {
        currentStep = -1;
        direction = 1;
        currentRepetinion = 0;
    }

    public bool GoToNext()
    {
        if(animationType == FingerAnimationType.Once)
        {
            currentStep++;

            if (currentStep > animationPoints.Length -1) // animation finished
                return false;
        }
        else if(animationType == FingerAnimationType.Loop)
        {
            currentStep++;
            if (currentStep > animationPoints.Length - 1)
            {
                currentStep = 0;
                IncrementRepetition();
            }
        }
        else if (animationType == FingerAnimationType.Ping_pong)
        {
            currentStep = currentStep + direction;

            if (currentStep >= animationPoints.Length - 1)
            {
                direction = -direction;
                IncrementRepetition();
            } else if ( currentStep <= -1)
            {
                currentStep = 0;
                direction = -direction;
                IncrementRepetition();
            }
        }

        //It's repeated enough
        if (currentRepetinion >= numberOfRepetition)
        {
            Debug.Log("Enough repetition");
            return false;
        }

        animationPoints[currentStep].SendAngleToArduino();


        return true;
    }

    void IncrementRepetition()
    {
        currentRepetinion++;
        Debug.Log("Current repetition " + currentRepetinion + "/" + numberOfRepetition);
    }
}

[ExecuteInEditMode]
public class FingerAnimationController : MonoBehaviour {

    public bool autoStart = true;
    public int globalSpedd = 50;

    public string debugAnimationName;

    public FingerJoint[] joints = new FingerJoint[3];
    public FingerAnimation[] animations = new FingerAnimation[3];

    [HideInInspector]
    FingerAnimation currentAnimationPlaying = null;

    void Start()
    {
        ClearAnimation();
        UduinoManager.Instance.SetReadCallback(ReadCallback);
        UduinoManager.Instance.OnBoardConnected += OnBoardCo;
    }

    void OnBoardCo(UduinoDevice name)
    {
        SetGlobalSpeed();
     //   PlayAnimation("test");
    }

    public void ClearAnimation()
    {
        currentAnimationPlaying = null;
    }

    public void PlayAnimation(string animationName)
    {
        FingerAnimation animationToPlay = GetAnimation(animationName);

        if(currentAnimationPlaying != null)
        {
            Debug.Log("The animation " + currentAnimationPlaying.animationName + " is currently playing.");
        } else
        {
            currentAnimationPlaying = animationToPlay;
            if (autoStart)
                StartAnimation();
        }
    }

    public void StartAnimation()
    {
        currentAnimationPlaying.Init();
        GoToNextStep();
    }

    public void MovementFinished()
    {
        if (currentAnimationPlaying != null)
        {
            if (GoToNextStep())
            {
                Debug.Log("Go to next animation step");
            }
            else
            {
                AnimationStopped();
            }
        } else
        {
            Debug.Log("Not animation loaded");
        }
    }

    public bool GoToNextStep()
    {
        if (currentAnimationPlaying != null)
        {
            return currentAnimationPlaying.GoToNext();
        }
        return false;
    }

    public void AnimationStopped()
    {
        Debug.Log("Animation stopping");
        currentAnimationPlaying.Stop();
        currentAnimationPlaying = null;
    }

    public FingerAnimation GetAnimation(string animationName)
    {
        foreach(FingerAnimation animation in animations)
        {
            if (animation.animationName == animationName)
                return animation;
        }
        return null;
    }

    public void StartMotor()
    {
        UduinoManager.Instance.sendCommand("start");
    }
    public void StopMotor()
    {
        UduinoManager.Instance.sendCommand("stop");
    }

    public void Calibrate()
    {
        UduinoManager.Instance.sendCommand("calibrate");
    }

    public void SetGlobalSpeed()
    {
        UduinoManager.Instance.sendCommand("speed", globalSpedd);
    }

    public void ValueReceived(string data, string board)
    {
        Debug.Log(data);
    }


    public void ReadCallback(string message)
    {
        //Debug.Log(message);

        if (message == "Y")
        {
            Debug.Log("Arrived!");
            MovementFinished();
        }
        else
        {
           string[] splittedAngles =  message.Split(' ');
            if(splittedAngles[0] == "a")
            {
                //ReadingAngles
                for(int i=1; i < splittedAngles.Length -1 ;i++ )
                {
                    string[] angleMessage = splittedAngles[i].Split(':');
                    try
                    {
                        int id = int.Parse(angleMessage[0]);
                        int angle = int.Parse(angleMessage[1]);
                        joints[id].SetAngle(angle);
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log(e);
                    }
                }
            } else
            {
                  Debug.Log(message);
            }
        }
    }
}