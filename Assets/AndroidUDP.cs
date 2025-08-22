#if UNITY_ANDROID || UNITY_EDITOR
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
        // Only run this code at runtime on Android devices
        if (Application.platform == RuntimePlatform.Android)
        {
            udp = new UdpClient();
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0)); // allow receiving too
            udp.BeginReceive(ReceiveCallback, null);
            SendCommand("HelloFromTablet");
        }
    }

    public void ReSendHello()
    {
        SendCommand("HelloFromTablet");
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
            string teamInfoJson = message.Substring("TeamData:".Length);
            Debug.Log("Tablet: Got team info JSON - " + teamInfoJson);

            TeamDataListWrapper wrapper = JsonUtility.FromJson<TeamDataListWrapper>(teamInfoJson);

            if (wrapper != null && wrapper.teamScores != null)
            {
                // update your UI
                androidUI.SaveTeamData(wrapper.teamScores);
            }
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