#if UNITY_STANDALONE || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System;
using System.Collections.Generic;

public class PCServer : MonoBehaviour
{
    public TMBridgeFieldsetController tmBridgeController; // Reference to the TM Bridge controller
    public LeaderboardManager leaderboardManager; // Reference to the leaderboard manager
    private UdpClient udp;
    private IPEndPoint tabletEndpoint; // we'll store tablet's address when it first connects

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

        Debug.Log("PC Received: " + message);

        if (message == "HelloFromTablet")
        {
            // ip contains tablet's IP and source port
            tabletEndpoint = ip;
            SendToTablet("HelloAck");
            SendToTablet(leaderboardManager.GetTeamDataMessage());
        }

        // Handle the command
        HandleCommand(message);

        // Keep listening
        udp.BeginReceive(ReceiveCallback, null);
    }

    private void HandleCommand(string message)
    {
        // Example: handle tablet commands
        if (message.StartsWith("CreateMatch"))
        {
            Debug.Log("PC: Match creation request: " + message);

            // Split the message by '|'
            string[] parts = message.Split('|');

            // Use LeaderboardManager.Instance
            var lm = LeaderboardManager.Instance;

            // Reset fields first if needed
            lm.Red1 = lm.Red2 = lm.Blue1 = lm.Blue2 = "";
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
            if (lm.runAuton)
            {
                tmBridgeController.FullReset();
            }
            else
            {
                tmBridgeController.SkipAuton();
            }
            Debug.Log($"Match Setup: Red1={lm.Red1}, Red2={lm.Red2}, Blue1={lm.Blue1}, Blue2={lm.Blue2}, Auton={lm.runAuton}, Record={lm.recordScores}");
        }
        else if (message == "EndMatch")
        {
            Debug.Log("PC: End match requested");
            tmBridgeController.EndEarlyMatch();
        }
        else if (message == "StartMatch")
        {
            Debug.Log("PC: Start match requested");
            tmBridgeController.StartMatch();
        } else if (message == "SkipAuton") {
            Debug.Log("PC: Skip auton requested");
            tmBridgeController.SkipAuton();
        } else if (message == "FullReset") {
            Debug.Log("PC: Full reset requested");
            tmBridgeController.FullReset();
        }
    }

    // Send data back to the tablet
    public void SendToTablet(string message)
    {
        if (tabletEndpoint != null)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udp.Send(data, data.Length, tabletEndpoint);
            Debug.Log("PC Sent: " + message);
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
