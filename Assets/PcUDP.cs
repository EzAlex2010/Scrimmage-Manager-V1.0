#if UNITY_STANDALONE || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class PCServer : MonoBehaviour
{
    public TMBridgeFieldsetController tmBridgeController; // Reference to the TM Bridge controller
    public LeaderboardManager leaderboardManager; // Reference to the leaderboard manager
    private UdpClient udp;
    private IPEndPoint tabletEndpoint; // we'll store tablet's address when it first connects
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

    void Start()
    {
        udp = new UdpClient(9050); // Listen on port 9050
        udp.BeginReceive(ReceiveCallback, null);
        Debug.Log("PC Server started on port 9050");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, 9050);
        byte[] data = udp.EndReceive(ar, ref ip);
        string message = Encoding.UTF8.GetString(data);

        // Handle the command on the main thread
        RunOnMainThread(() => {
            Debug.Log("PC Received: " + message);
            if (message == "HelloFromTablet")
            {
                // ip contains tablet's IP and source port
                tabletEndpoint = ip;
                SendToTablet("HelloAck");
                SendToTablet(leaderboardManager.GetTeamDataMessage());
                SendToTablet("Passcode:" + leaderboardManager.passcode);
            }
            HandleCommand(message);
        });

        // Keep listening
        udp.BeginReceive(ReceiveCallback, null);
    }

    private void HandleCommand(string message)
    {
        // Example: handle tablet commands
        if (message.StartsWith("CreateMatch"))
        {
            Debug.Log("PC: Match creation request: " + message);
            string[] parts = message.Split('|'); // Split the message by '|'
            // Use LeaderboardManager.Instance
            var lm = LeaderboardManager.Instance;
            lm.Red1 = lm.Red2 = lm.Blue1 = lm.Blue2 = ""; // Reset fields first if needed
            lm.runAuton = true;
            lm.recordScores = false;
            foreach (var part in parts)
            {
                if (part.StartsWith("Red1:"))
                    lm.Red1 = part.Substring("Red1:".Length);
                else if (part.StartsWith("Red2:"))
                    lm.Red2 = part.Substring("Red2:".Length);
                else if (part.StartsWith("Blue1:"))
                    lm.Blue1 = part.Substring("Blue1:".Length);
                else if (part.StartsWith("Blue2:"))
                    lm.Blue2 = part.Substring("Blue2:".Length);
                else if (part.StartsWith("Auton:"))
                    lm.runAuton = part.Substring("Auton:".Length).ToLower() == "true";
                else if (part.StartsWith("Record:"))
                    lm.recordScores = part.Substring("Record:".Length).ToLower() == "true";
            }
            Debug.Log("Settingup match...");
            StartCoroutine(tmBridgeController.HandleMatchStart(lm.runAuton));
            Debug.Log($"Match Setup: Red1={lm.Red1}, Red2={lm.Red2}, Blue1={lm.Blue1}, Blue2={lm.Blue2}, Auton={lm.runAuton}, Record={lm.recordScores}");
        }
        else if (message == "EndMatchEarly")
        {
            Debug.Log("PC: End match period requested");
            tmBridgeController.EndEarlyMatch();
        }
        else if (message == "StartMatch")
        {
            Debug.Log("PC: Start match requested");
            tmBridgeController.StartMatch();
        }
    }

    // Send data back to the tablet
    public void SendToTablet(string message)
    {
        if (tabletEndpoint != null)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udp.Send(data, data.Length, tabletEndpoint);
            if (!message.StartsWith("MatchState")&&!message.StartsWith("MatchTime")) Debug.Log("Pc Sent: " + message);
        } else {
            Debug.LogWarning("PC: No tablet connected to send data to.");
        }
    }

    void OnApplicationQuit()
    {
        udp.Close();
    }
}
#endif
