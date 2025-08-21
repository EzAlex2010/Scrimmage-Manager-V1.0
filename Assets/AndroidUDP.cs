#if UNITY_ANDROID
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System;

public class TabletClient : MonoBehaviour
{
    private UdpClient udp;
    public string pcIP = "192.168.1.100"; // Change this to your PC's LAN IP
    private int pcPort = 9050;

    public AndroidUI androidUI; // Reference to your AndroidUI script

    void Start()
    {
        udp = new UdpClient();
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0)); // allow receiving too
        udp.BeginReceive(ReceiveCallback, null);
    }

    public void SendCommand(string command)
    {
        byte[] data = Encoding.UTF8.GetBytes(command);
        udp.Send(data, data.Length, pcIP, pcPort);
        Debug.Log("Tablet Sent: " + command);
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = udp.EndReceive(ar, ref ip);
        string message = Encoding.UTF8.GetString(data);

        Debug.Log("Tablet Received: " + message);

        // Handle PC updates
        HandleUpdate(message);

        // Keep listening
        udp.BeginReceive(ReceiveCallback, null);
    }

    private void HandleUpdate(string message)
    {
        if (message.StartsWith("TeamData:"))
        {
            string teamInfo = message.Substring("TeamData:".Length);
            Debug.Log("Tablet: Got team info - " + teamInfo);

            // TODO: Update your tablet UI with this info
        }
        if (message == "MatchEnded")
        {
            androidUI.EndMatch();
        }
    }

    void OnApplicationQuit()
    {
        udp.Close();
    }
}
#endif