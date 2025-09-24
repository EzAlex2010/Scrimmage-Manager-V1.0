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
    public TMBridgeFieldsetController tmBridgeController;
    public LeaderboardManager leaderboardManager;
    public int port = 9051; // <-- configurable port
    private UdpClient udp;
    private IPEndPoint tabletEndpoint;
    private static readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();
    public static void RunOnMainThread(Action action)
    {
        actions.Enqueue(action);
    }

    void Update()
    {
        while (actions.TryDequeue(out var action))
            action?.Invoke();
    }

    void Start()
    {
        try
        {
            if (udp != null)
            {
                udp.Close();
                udp = null;
            }
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            udp.BeginReceive(ReceiveCallback, null);
            Debug.Log($"PC Server started on port {port}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start PC Server: " + ex);
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, port);
        byte[] data = udp.EndReceive(ar, ref ip);
        string message = Encoding.UTF8.GetString(data);

        RunOnMainThread(() => {
            Debug.Log("PC Received: " + message);
            if (message == "HelloFromTablet")
            {
                tabletEndpoint = ip;
                SendToTablet("HelloAck");
                SendToTablet(leaderboardManager.GetTeamDataMessage());
                SendToTablet("Passcode:" + leaderboardManager.passcode);
            }
            HandleCommand(message);
        });

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
            bool redWinPoint = false, blueWinPoint = false;

            foreach (var part in parts)
            {
                if (part.StartsWith("Red1:")) red1 = part.Substring("Red1:".Length);
                else if (part.StartsWith("Red2:")) red2 = part.Substring("Red2:".Length);
                else if (part.StartsWith("Blue1:")) blue1 = part.Substring("Blue1:".Length);
                else if (part.StartsWith("Blue2:")) blue2 = part.Substring("Blue2:".Length);
                else if (part.StartsWith("RedScore:")) int.TryParse(part.Substring("RedScore:".Length), out redScore);
                else if (part.StartsWith("BlueScore:")) int.TryParse(part.Substring("BlueScore:".Length), out blueScore);
                else if (part.StartsWith("RedWinPoint:")) redWinPoint = part.Substring("RedWinPoint:".Length) == "1";
                else if (part.StartsWith("BlueWinPoint:")) blueWinPoint = part.Substring("BlueWinPoint:".Length) == "1";
            }

            var lm = LeaderboardManager.Instance;
            lm.matchData.Add(new MatchData
            {
                Red1 = red1,
                Red2 = red2,
                Blue1 = blue1,
                Blue2 = blue2,
                RedScore = redScore,
                BlueScore = blueScore,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
            lm.SaveMatches();
            if (redScore > blueScore)
            {
                lm.AddWinPoints(red1, 2 + (redWinPoint ? 1 : 0), redScore, true);
                lm.AddWinPoints(red2, 2 + (redWinPoint ? 1 : 0), redScore, true);
                lm.AddWinPoints(blue1, 0 + (blueWinPoint ? 1 : 0), blueScore, false);
                lm.AddWinPoints(blue2, 0 + (blueWinPoint ? 1 : 0), blueScore, false);
            }
            else if (blueScore > redScore)
            {
                lm.AddWinPoints(blue1, 2 + (blueWinPoint ? 1 : 0), blueScore, true);
                lm.AddWinPoints(blue2, 2 + (blueWinPoint ? 1 : 0), blueScore, true);
                lm.AddWinPoints(red1, 0 + (redWinPoint ? 1 : 0), redScore, false);
                lm.AddWinPoints(red2, 0 + (redWinPoint ? 1 : 0), redScore, false);
            }
            else // tie
            {
                lm.AddWinPoints(red1, 1 + (redWinPoint ? 1 : 0), redScore, false);
                lm.AddWinPoints(red2, 1 + (redWinPoint ? 1 : 0), redScore, false);
                lm.AddWinPoints(blue1, 1 + (blueWinPoint ? 1 : 0), blueScore, false);
                lm.AddWinPoints(blue2, 1 + (blueWinPoint ? 1 : 0), blueScore, false);
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
