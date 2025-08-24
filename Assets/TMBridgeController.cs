#if UNITY_STANDALONE || UNITY_EDITOR
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Diagnostics;
using System.Collections.Concurrent;
using System;

public class TMBridgeFieldsetController : MonoBehaviour
{
    // Customize this to your TM Bridge setup
    public string tmBridgeBaseUrl = "http://localhost:8000";
    public string fieldsetName = "field1"; // Name from TM Bridge
    public float pollIntervalSeconds = 0.5f;
    public float postMatchDelaySeconds = 3f;
    public string matchState = "";
    public float matchTime;
    private bool canPoll = true; // Controls whether polling should happen
    private Process tmBridgeProcess;
    public bool sendPollToLog; // Toggle for logging polling results
    public string displayMatchState = "Unknown"; // Default state for display
    public bool autoUpdateState;
    //public Button StartButton;
    //public Button EndEarlyButton;
    //public Button AbortButton;
    public LeaderboardManager leaderboardManager;
    public bool autonStarted;
    public PCServer pcServer;

    private static readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();

    public static void RunOnMainThread(Action action)
    {
        actions.Enqueue(action);
    }

    void Update()
    {
        while (actions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }


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
        UnityEngine.Debug.Log("Starting Match");
        StartCoroutine(SendFieldsetCommand("start"));
        pcServer.SendToTablet("Button:Enable:EndEarly");
        pcServer.SendToTablet("Button:Disable:Start");
        UnityEngine.Debug.Log("About to set displayMatchState...");
        if (displayMatchState == "AUTONOMOUS")
        {
            displayMatchState = "DRIVER CONTROL";
        }
        else
        {
            displayMatchState = "AUTONOMOUS";
        }
        UnityEngine.Debug.Log("Match Started");
    }

    public void StopMatch()
    {
        StartCoroutine(SendFieldsetCommand("abort"));
        ResetTimer();
    }

    public void ResetTimer()
    {
        StartCoroutine(SendFieldsetCommand("reset"));
        UnityEngine.Debug.Log("Timer Reset");
    }

    public void EndEarlyMatch()
    {
        UnityEngine.Debug.Log("Ending Match Period Early");
        if (matchState == "DRIVER CONTROL")
        {
            StartCoroutine(EndEarlyAndEnd());
        }
        else
        {
            StartCoroutine(SendFieldsetCommand("end-early"));
            pcServer.SendToTablet("Button:Enable:Start");
        }
        pcServer.SendToTablet("Button:Disable:EndEarly"); // Disable button after use
    }

    private IEnumerator EndEarlyAndEnd()
    {
        // Send the command first
        yield return StartCoroutine(SendFieldsetCommand("end-early"));

        // Wait 1–2 seconds
        yield return new WaitForSeconds(1f);  // change 2f to 1f if you want 1 second

        // Send A Match Ended message to the leaderboard manager here
        pcServer.SendToTablet("MatchEnded");
        UnityEngine.Debug.Log("[TM] Match ended.");
    }

    public IEnumerator SkipAuton()
    {
        yield return StartCoroutine(FullReset());
        yield return new WaitForSeconds(3f);
        displayMatchState = "Skipping Auton";
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
        yield return StartCoroutine(SendFieldsetCommand("start"));
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(SendFieldsetCommand("end-early"));
    }

    public IEnumerator FullReset()
    {
        displayMatchState = "Resetting";
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
        yield return StartCoroutine(SendFieldsetCommand("start"));
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(SendFieldsetCommand("abort"));
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(SendFieldsetCommand("reset"));
    }

    public IEnumerator HandleMatchStart(bool runAuton)
    {
        UnityEngine.Debug.Log("[TM] Setting up match...");
        pcServer.SendToTablet("Button:Disable:Start");
        pcServer.SendToTablet("Button:Disable:EndEarly");
        pcServer.SendToTablet("Button:Disable:Abort");
        if (runAuton)
        {
            yield return StartCoroutine(FullReset());
        }
        else
        {
            yield return StartCoroutine(SkipAuton());
        }

        // These run only after the coroutine above finishes
        autoUpdateState = true;
        displayMatchState = "Ready to Start";
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
        pcServer.SendToTablet("Button:Enable:Start");
        pcServer.SendToTablet("Button:Enable:Abort");
        UnityEngine.Debug.Log("[TM] Match setup complete. Ready to start.");
    }

    void Start()
    {
        StartTMBridge();
        UnityEngine.Debug.LogWarning("Bridge Is Disabled!");
        if (matchState != "paused") ResetTimer();
        StartCoroutine(PollMatchTime());
    }

    public void UpdateMatchState()
    {
        if (matchState == "AUTONOMOUS") displayMatchState = "AUTONOMOUS";
        else if (matchState == "DRIVER CONTROL") displayMatchState = "DRIVER CONTROL";
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
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
                RunOnMainThread(() => {
                    string json = request.downloadHandler.text;
                    FieldsetStatus status = JsonUtility.FromJson<FieldsetStatus>(json);
                    if (sendPollToLog) UnityEngine.Debug.Log($"[TM] Time: {status.match_timer_content} | State: {status.match_state}");
                    pcServer.SendToTablet($"MatchTime:{status.match_timer_content}");
                    matchState = status.match_state;
                    matchTime = status.match_time;
                    if (autoUpdateState)
                    {
                        UpdateMatchState();
                    }
                    if (status.match_time == 0 && matchState == "DRIVER CONTROL")
                    {
                        pcServer.SendToTablet("MatchEnded");
                        UnityEngine.Debug.Log("[TM] Match ended.");
                    }
                    if (status.match_time == 15 && displayMatchState == "AUTONOMOUS") autonStarted = true;
                    if (status.match_time == 0 && displayMatchState == "AUTONOMOUS" && autonStarted)
                    {
                        pcServer.SendToTablet("Button:Enable:Start");
                        pcServer.SendToTablet("Button:Disable:EndEarly");
                        UnityEngine.Debug.Log("[TM] Auton ended.");
                    }
                });
                
            }
            else
            {
                UnityEngine.Debug.LogError($"[TM] Polling failed: {request.error}");
                canPoll = false; // Stop further polling until reset
            }
            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private IEnumerator SendFieldsetCommand(string command)
    {
        UnityEngine.Debug.Log($"[SendFieldsetCommand] Entered with command = {command}");
        string url = $"{tmBridgeBaseUrl}/api/fieldset/{fieldsetName}/{command}";
        UnityEngine.Debug.Log($"[SendFieldsetCommand] URL = {url}");

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

    public void EndMatch()
    {
        autoUpdateState = false;
        displayMatchState = "Match Ended";
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
    }
}
#endif