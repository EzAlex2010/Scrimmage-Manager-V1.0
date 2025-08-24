#if UNITY_ANDROID || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using TMPro;

public class TabletClient : MonoBehaviour
{
    private UdpClient udp;
    private IPEndPoint pcEndPoint;
    private bool registered = false;
    public AndroidUI androidUI; // Reference to your UI script
    public GameObject connectiontext;
    private int pcPort = 9050;     // PC listening port

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

        RunOnMainThread(() => {
            if (!registered)
            {
                registered = true;
                pcEndPoint = remoteEP; // save the endpoint to send future messages
                Debug.Log("Tablet: registered with PC at " + pcEndPoint.Address + ":" + pcEndPoint.Port);
            }
            HandleUpdate(message);
        });
        // Keep listening
        udp.BeginReceive(ReceiveCallback, null);
    }

    private void HandleUpdate(string message)
    {
        connectiontext.SetActive(false);
        if (!message.StartsWith("MatchState") && !message.StartsWith("MatchTime")) Debug.Log("Received: " + message);
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
            else if (message == "MatchEnded")
            {
                androidUI.EndMatch();
            }
            else if (message.StartsWith("MatchState:"))
            {
                string state = message.Substring("MatchState:".Length);
                androidUI.UpdateMatchState(state);
            }
            else if (message.StartsWith("MatchTime:"))
            {
                string time = message.Substring("MatchTime:".Length);
                androidUI.UpdateMatchTime(time);
            }
            else if (message.StartsWith("Button:"))
            {
                string button = message.Substring("Button:".Length);
                Debug.Log(button);
                if (button.StartsWith("Disable:"))
                {
                    string btnName = button.Substring("Disable:".Length);
                    androidUI.SetButtonInteractable(btnName, false);
                }
                else if (button.StartsWith("Enable:"))
                {
                    string btnName = button.Substring("Enable:".Length);
                    androidUI.SetButtonInteractable(btnName, true);
                }
            }
    }

    void OnApplicationQuit()
    {
        if (udp != null) udp.Close();
    }
}
#endif