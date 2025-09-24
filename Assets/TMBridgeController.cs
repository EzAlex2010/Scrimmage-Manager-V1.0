#if UNITY_STANDALONE || UNITY_EDITOR
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Diagnostics;
using System.Collections.Concurrent;
using System;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;

public class TMBridgeFieldsetController : MonoBehaviour
{
    // Customize this to your TM Bridge setup
    public string tmBridgeBaseUrl = "http://localhost:8000";
    public string fieldsetName = "field1"; // Name from TM Bridge
    private string tmBridgeExePath = "Dependencies/vex-tm-bridge.exe";
    private string tmBridgeArgs = "--tm-host-ip localhost --competition V5RC --port 8000";
    public float pollIntervalSeconds = 0.5f;
    public float postMatchDelaySeconds = 3f;
    public string matchState = "";
    public float matchTime;
    private bool canPoll = true; // Controls whether polling should happen
    private Process tmBridgeProcess;
    public bool sendPollToLog; // Toggle for logging polling results
    public string displayMatchState = "Unknown"; // Default state for display
    public bool autoUpdateState;
    public Button StartButton;
    public Button EndEarlyButton;
    public Button AbortButton;
    public TMP_Text MatchStateText;
    public TMP_Text MatchTimeText;
    public LeaderboardManager leaderboardManager;
    public bool autonStarted;
    public PCServer pcServer;
    public string matchTimeNice;
    private string configFilePath;
    public DependencyChecker dependencyChecker;


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

    public static class Win32Popup
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        /// <summary>
        /// Shows a Windows message box.
        /// type: 0 = OK, 1 = OK/Cancel, 2 = Abort/Retry/Ignore, 3 = Yes/No/Cancel, 4 = Yes/No, etc.
        /// </summary>
        public static void Show(string message, string title = "Notice", uint type = 0)
        {
            MessageBox(IntPtr.Zero, message, title, type);
        }
    }

    private void LoadConfig()
    {
        configFilePath = Path.Combine(Application.persistentDataPath, "tmbridge_config.txt");

        if (!File.Exists(configFilePath))
        {
            string defaultConfig =
    @"fieldsetName=field1
port=8001";
            File.WriteAllText(configFilePath, defaultConfig);
        }

        string fieldName = "field1";
        int port = 8000;

        foreach (string line in File.ReadAllLines(configFilePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            string[] parts = line.Split(new char[] { '=' }, 2);
            if (parts.Length != 2) continue;
            string key = parts[0].Trim();
            string value = parts[1].Trim();
            switch (key)
            {
                case "fieldsetName":
                    fieldName = value;
                    break;
                case "port":
                    if (int.TryParse(value, out int p))
                        port = p;
                    break;
            }
        }

        fieldsetName = fieldName;
        tmBridgeBaseUrl = $"http://localhost:{port}";
        tmBridgeArgs = $"--tm-host-ip localhost --competition V5RC --port {port}";
    }

    void StartTMBridge()
    {
        string pythonPath = DependencyChecker.GetPythonPath();
        if (pythonPath == null)
        {
            Win32Popup.Show("Python 3.10+ was not found.\n\n" +
                            "Please install it from:\nhttps://www.python.org/downloads/",
                            "Missing Python");
            return;
        }
        if (!DependencyChecker.CheckTmBridgeInstalled(pythonPath))
        {
            Win32Popup.Show("vex-tm-bridge is not installed.\n\n" +
                            "Run:\n   pip install vex-tm-bridge",
                            "Missing TM-Bridge");
            return;
        }

        // Use config port
        string argsWithPort = tmBridgeArgs;

        // Wrap in cmd.exe /K to keep window open
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K \"{pythonPath}\" -m vex_tm_bridge {argsWithPort}",
            UseShellExecute = true,   // required for 'runas'
            Verb = "runas",           // elevate to admin
            CreateNoWindow = false,
            WorkingDirectory = Application.dataPath
        };

        try
        {
            tmBridgeProcess = Process.Start(psi);
        }
        catch (Exception e)
        {
            Win32Popup.Show("Failed to start TM-Bridge:\n" + e.Message, "Error");
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
        ButtonControl("Start", false);
        ButtonControl("EndEarly", true);
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
            ButtonControl("Start", true);
        }
        ButtonControl("EndEarly", false); // Disable button after use
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
        ButtonControl("Start", false);
        ButtonControl("EndEarly", false);
        ButtonControl("Abort", false);
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
        ButtonControl("Start", true);
        ButtonControl("Abort", true);
        UnityEngine.Debug.Log("[TM] Match setup complete. Ready to start.");
    }

    void Start()
    {
        LoadConfig();
        StartTMBridge();
        if (matchState != "paused") ResetTimer();
        StartCoroutine(PollMatchTime());
    }

    public void UpdateMatchState()
    {
        if (matchState == "AUTONOMOUS") displayMatchState = "AUTONOMOUS";
        else if (matchState == "DRIVER CONTROL") displayMatchState = "DRIVER CONTROL";
        UpdateStateAndTime();
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
                RunOnMainThread(() =>
                {
                    string json = request.downloadHandler.text;
                    FieldsetStatus status = JsonUtility.FromJson<FieldsetStatus>(json);
                    if (sendPollToLog) UnityEngine.Debug.Log($"[TM] Time: {status.match_timer_content} | State: {status.match_state}");
                    matchState = status.match_state;
                    matchTime = status.match_time;
                    matchTimeNice = status.match_timer_content;
                    if (autoUpdateState)
                    {
                        UpdateMatchState();
                    }
                    else UpdateStateAndTime();
                    if (status.match_time == 0 && matchState == "DRIVER CONTROL")
                    {
                        pcServer.SendToTablet("MatchEnded");
                        UnityEngine.Debug.Log("[TM] Match ended.");
                    }
                    if (status.match_time == 15 && displayMatchState == "AUTONOMOUS") autonStarted = true;
                    if (status.match_time == 0 && displayMatchState == "AUTONOMOUS" && autonStarted)
                    {
                        ButtonControl("Start", true);
                        ButtonControl("EndEarly", false);
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

    public void EndMatch()
    {
        autoUpdateState = false;
        displayMatchState = "Match Ended";
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
    }

    public void ButtonControl(string buttonName, bool interactable)
    {
        if (buttonName == "Start")
        {
            StartButton.interactable = interactable;
            if (interactable)
                pcServer.SendToTablet("Button:Enable:Start");
            else
                pcServer.SendToTablet("Button:Disable:Start");
        }
        else if (buttonName == "EndEarly")
        {
            EndEarlyButton.interactable = interactable;
            if (interactable)
                pcServer.SendToTablet("Button:Enable:EndEarly");
            else
                pcServer.SendToTablet("Button:Disable:EndEarly");
        }
        else if (buttonName == "Abort")
        {
            AbortButton.interactable = interactable;
            if (interactable)
                pcServer.SendToTablet("Button:Enable:Abort");
            else
                pcServer.SendToTablet("Button:Disable:Abort");
        }
    }

    public void UpdateStateAndTime()
    {
        pcServer.SendToTablet($"MatchState:{displayMatchState}");
        pcServer.SendToTablet($"MatchTime:{matchTimeNice}");
        MatchStateText.text = displayMatchState;
        MatchTimeText.text = matchTimeNice;
    }
}
#endif