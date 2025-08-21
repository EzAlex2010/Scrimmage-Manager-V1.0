#if UNITY_STANDALONE || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System;

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

        // Save tablet endpoint so we can send back to it
        tabletEndpoint = ip;

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
            // TODO: Parse Red1/Blue1/etc. from message and call your match manager
        }
        else if (message == "EndMatch")
        {
            Debug.Log("PC: End match requested");
            // Call StopMatch()
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
        }
    }

    void OnApplicationQuit()
    {
        udp.Close();
    }
}
#endif
