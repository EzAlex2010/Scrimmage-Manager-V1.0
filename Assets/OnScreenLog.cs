#if UNITY_ANDROID
using UnityEngine;
using TMPro; // or TMPro if you want TMP instead

public class OnScreenLogger : MonoBehaviour
{
    public TMP_Text logText; // assign a UI.Text object in the Canvas
    private string logBuffer = "";
    private int maxLines = 20; // limit how many lines are shown

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logBuffer += logString + "\n";

        // Keep only the last N lines
        string[] lines = logBuffer.Split('\n');
        if (lines.Length > maxLines)
        {
            logBuffer = string.Join("\n", lines, lines.Length - maxLines, maxLines);
        }

        logText.text = logBuffer;
    }
}
#endif