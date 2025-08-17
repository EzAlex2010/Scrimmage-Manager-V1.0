using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
using System.Text.RegularExpressions;
using System.Diagnostics;

public class TMBridgeFieldsetController : MonoBehaviour
{
    // Customize this to your TM Bridge setup
    public string tmBridgeBaseUrl = "http://localhost:8000";
    public string fieldsetName = "field1"; // Name from TM Bridge
    public float pollIntervalSeconds = 0.5f;
    public float postMatchDelaySeconds = 3f;
    public string matchState = "";
    public float matchTime;
    public TextMeshProUGUI matchTimerText;
    public TextMeshProUGUI matchStateText;
    private bool hasTriggeredReset = false;
    private bool canPoll = true; // Controls whether polling should happen
    private Process tmBridgeProcess;

    void StartTMBridge()
    {
        if (Process.GetProcessesByName("vex-tm-bridge").Length == 0)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Application.dataPath + "/Dependencies/vex-tm-bridge.exe",
                Arguments = "--tm-host-ip localhost --competition V5RC --port 8000",
                UseShellExecute = true,   // required for admin
                Verb = "runas",           // triggers UAC
                WindowStyle = ProcessWindowStyle.Minimized
            };

            tmBridgeProcess = Process.Start(startInfo);
        }
    }

    void OnApplicationQuit()
    {
        StopTMBridge();
    }

    void OnDestroy()
    {
        StopTMBridge();
    }

    void StopTMBridge()
    {
        if (tmBridgeProcess != null && !tmBridgeProcess.HasExited)
        {
            tmBridgeProcess.Kill();
            tmBridgeProcess.Dispose();
            tmBridgeProcess = null;
        }
    }

    public void StartMatch()
    {
        StartCoroutine(SendFieldsetCommand("start"));
    }

    public void StopMatch()
    {
        StartCoroutine(SendFieldsetCommand("abort"));
        ResetTimer();
    }

    public void ResetTimer()
    {
        StartCoroutine(SendFieldsetCommand("reset"));
        hasTriggeredReset = false;
    }

    public void EndEarlyMatch()
    {
        StartCoroutine(SendFieldsetCommand("end-early"));
    }

    void Start()
    {
        StartTMBridge();
        UnityEngine.Debug.LogWarning("Bridge Is Disabled!");
        if (matchState != "paused") ResetTimer();
        StartCoroutine(PollMatchTime());
    }

    private IEnumerator PollMatchTime()
    {
        while (true)
        {
            if (!canPoll)
            {
                yield return null; // Wait until next frame, skip polling
                continue;
            }
            string url = $"{tmBridgeBaseUrl}/api/fieldset/{fieldsetName}";
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                FieldsetStatus status = JsonUtility.FromJson<FieldsetStatus>(json);
                UnityEngine.Debug.Log($"[TM] Time: {status.match_timer_content} | State: {status.match_state}");
                if (matchTimerText != null)
                {
                    matchTimerText.text = status.match_timer_content;
                    matchStateText.text = status.match_state;
                    matchState = status.match_state;
                    matchTime = status.match_time;
                }
                if (status.match_time == 0 && !hasTriggeredReset)
                {
                    hasTriggeredReset = true;
                    StartCoroutine(DelayedResetTimer(postMatchDelaySeconds));
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"[TM] Polling failed: {request.error}");
                canPoll = false; // Stop further polling until reset
            }
            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private IEnumerator DelayedResetTimer(float delay)
    {
        UnityEngine.Debug.Log("[TM] Match ended — waiting to reset...");
        yield return new WaitForSeconds(delay);
        //ResetTimer();
    }

    private IEnumerator SendFieldsetCommand(string command)
    {
        string url = $"{tmBridgeBaseUrl}/api/fieldset/{fieldsetName}/{command}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(new byte[0]);
        request.downloadHandler = new DownloadHandlerBuffer();

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.Log($"Successfully sent '{command}' to {fieldsetName}");
        }
        else
        {
            UnityEngine.Debug.LogError($"Failed to send command '{command}'\n{request.error}");
        }
    }
}


