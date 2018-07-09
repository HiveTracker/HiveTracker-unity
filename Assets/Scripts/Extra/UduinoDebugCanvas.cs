using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;

public class AndroidDebugCanvas : MonoBehaviour
{
    string logContent = "";
    Queue logQueue = new Queue();
    Text LogText = null;
    public static AndroidDebugCanvas Instance;

    void Awake()
    {
        Instance = this;
        CreateCanvasAndText();
        Application.logMessageReceived += HandleLog;
    }

    public void Log(string m)
    {
        LogText.text += "\n" + m;
    }

    void OnEnable()
    {
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    public void Clear()
    {
        logContent = "";
        logQueue.Clear();
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logContent = logString;
        string newString = "\n [" + type + "] : " + logContent;
        logQueue.Enqueue(newString);
        if (type == LogType.Exception)
        {
            newString = "\n" + stackTrace;
            logQueue.Enqueue(newString);
        }
        logContent = string.Empty;
        foreach (string mylog in logQueue)
        {
            logContent += mylog;
        }
    }

    void Update()
    {
        if (LogText != null)
        {
            LogText.text = logContent;
            if (logContent.Split('\n').Length - 1 > Screen.height / 20) // Clear queue
                Clear();
        }
    }

    void CreateCanvasAndText()
    {
        this.transform.name = "UduinoDebugCanvas";
        Canvas canvas = this.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler cs = this.gameObject.AddComponent<CanvasScaler>();
        cs.scaleFactor = 1.0f;
        cs.dynamicPixelsPerUnit = 10f;
        canvas.sortingOrder = 500;

        GameObject textDebug = new GameObject();
        textDebug.name = "UduinoDebugText";
        textDebug.transform.parent = this.transform;
        LogText = textDebug.AddComponent<Text>();
        LogText.alignment = TextAnchor.UpperLeft;
        LogText.horizontalOverflow = HorizontalWrapMode.Wrap;
        LogText.verticalOverflow = VerticalWrapMode.Overflow;
        Font ArialFont = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        LogText.font = ArialFont;
        LogText.fontSize = 20;
        LogText.enabled = true;
        LogText.color = Color.white;

        textDebug.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.width,Screen.height);
        textDebug.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        textDebug.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
        textDebug.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
        textDebug.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

        this.gameObject.transform.localScale = new Vector3(
                                                1.0f / this.transform.localScale.x * 0.1f,
                                                1.0f / this.transform.localScale.y * 0.1f,
                                                1.0f / this.transform.localScale.z * 0.1f);
    }

}