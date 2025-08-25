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
        else if (message.StartsWith("MatchResult"))
        {
            Debug.Log("PC: Match result received: " + message);
            string[] parts = message.Split('|');
            string red1 = "", red2 = "", blue1 = "", blue2 = "";
            int redScore = 0, blueScore = 0;

            foreach (var part in parts)
            {
                if (part.StartsWith("Red1:")) red1 = part.Substring("Red1:".Length);
                else if (part.StartsWith("Red2:")) red2 = part.Substring("Red2:".Length);
                else if (part.StartsWith("Blue1:")) blue1 = part.Substring("Blue1:".Length);
                else if (part.StartsWith("Blue2:")) blue2 = part.Substring("Blue2:".Length);
                else if (part.StartsWith("RedScore:")) int.TryParse(part.Substring("RedScore:".Length), out redScore);
                else if (part.StartsWith("BlueScore:")) int.TryParse(part.Substring("BlueScore:".Length), out blueScore);
            }

            var lm = LeaderboardManager.Instance;
            if (redScore > blueScore)
            {
                lm.AddWinPoints(red1, 2, redScore, true);
                lm.AddWinPoints(red2, 2, redScore, true);
                lm.AddWinPoints(blue1, 0, blueScore, false);
                lm.AddWinPoints(blue2, 0, blueScore, false);
            }
            else if (blueScore > redScore)
            {
                lm.AddWinPoints(blue1, 2, blueScore, true);
                lm.AddWinPoints(blue2, 2, blueScore, true);
                lm.AddWinPoints(red1, 0, redScore, false);
                lm.AddWinPoints(red2, 0, redScore, false);
            }
            else // tie
            {
                lm.AddWinPoints(red1, 1, redScore, false);
                lm.AddWinPoints(red2, 1, redScore, false);
                lm.AddWinPoints(blue1, 1, blueScore, false);
                lm.AddWinPoints(blue2, 1, blueScore, false);
            }
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
