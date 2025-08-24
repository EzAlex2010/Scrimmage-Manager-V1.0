#if UNITY_ANDROID || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System;

public class TabletClient : MonoBehaviour
{
    private UdpClient udp;
    private IPEndPoint pcEndPoint;
    private bool registered = false;
    public AndroidUI androidUI; // Reference to your UI script

    private int tabletPort = 9051; // arbitrary free port
    private int pcPort = 9050;     // PC listening port

    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            udp = new UdpClient(); // no port binding
            udp.EnableBroadcast = true;
            udp.BeginReceive(ReceiveCallback, null);
            InvokeRepeating(nameof(SendHello), 0f, 3f);
        }
    }

    void SendHello()
    {
        if (!registered)
        {
            byte[] data = Encoding.UTF8.GetBytes("HelloFromTablet");
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, pcPort);
            udp.Send(data, data.Length, broadcastEP);
            Debug.Log("Tablet: broadcast hello...");
        }
    }

    public void SendCommand(string command)
    {
        if (pcEndPoint != null)
        {
            byte[] data = Encoding.UTF8.GetBytes(command);
            udp.Send(data, data.Length, pcEndPoint);
            Debug.Log("Tablet Sent: " + command);
        }
        else
        {
            Debug.LogWarning("Tablet: PC endpoint unknown, cannot send " + command);
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = udp.EndReceive(ar, ref remoteEP);
        string message = Encoding.UTF8.GetString(data);

        Debug.Log("Tablet Received: " + message);

        if (!registered)
        {
            registered = true;
            pcEndPoint = remoteEP; // save the endpoint to send future messages
            Debug.Log("Tablet: registered with PC at " + pcEndPoint.Address + ":" + pcEndPoint.Port);
        }

        // Handle incoming messages
        HandleUpdate(message);

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
        if (udp != null) udp.Close();
    }
}
#endif