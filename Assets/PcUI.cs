#if UNITY_STANDALONE || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI;
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;
    public List<TeamData> teamScores = new List<TeamData>();
    private string filePath;
    public string passcode = "6741 Robotics";

    public TMBridgeFieldsetController tmBridgeController; // Reference to the TM Bridge controller
    public PCServer pcServer; // Reference to your PCServer script
    public bool recordScores = false;
    public string Red1;
    public string Red2;
    public string Blue1;
    public string Blue2;
    public bool runAuton = true; // Flag to control auton mode. True by default
    public GameObject androidconnectiontext;
    public GameObject PcUI;
    public Transform scrollContent;      // Reference to the ScrollView Content object
    public GameObject teamDataPrefab;    // Reference to your TeamData_UI prefab

    public void test()
    {
        ClearScores();
        AddWinPoints("6741A", 10);
        AddWinPoints("6741R", 15);
        AddWinPoints("6741S", 2);
        AddWinPoints("6741D", 7);
        AddWinPoints("6741V", 0);
        AddWinPoints("6741F", 0);
        AddWinPoints("6741G", 0);
        AddWinPoints("6741X", 0);
        AddWinPoints("6741T", 0);
        AddWinPoints("6741W", 0);
        AddWinPoints("6741Z", 0);
        AddWinPoints("6741N", 0);
        AddWinPoints("6741M", 0);
    }

    public void UpdateLeaderboardDisplay()
    {
        Debug.Log("[AndroidUI] Updating leaderboard display...");
        // Clear previous entries
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        // Sort teamScores by winpoints descending
        teamScores.Sort((a, b) => b.winpoints.CompareTo(a.winpoints));

        // Instantiate prefabs for each team
        foreach (var team in teamScores)
        {
            GameObject item = Instantiate(teamDataPrefab, scrollContent);
            TeamData_UI ui = item.GetComponent<TeamData_UI>();
            ui.Setup(team);
        }
        Debug.Log("[AndroidUI] Leaderboard display updated.");
    }

    void Start()
    {
        androidconnectiontext.SetActive(false);
        PcUI.SetActive(true);
        LoadScores();
        UpdateLeaderboardDisplay();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        filePath = Application.persistentDataPath + "/leaderboard.json";
    }

    public string GetTeamDataMessage()
    {
        // Wrap the teamScores list in your existing wrapper so it serializes cleanly
        string json = JsonUtility.ToJson(new TeamDataListWrapper { teamScores = teamScores });
        Debug.Log("Serialized team data JSON: " + json);
        return "TeamData:" + json;
    }

    public void AddWinPoints(string teamName, int winPoints)
    {
        TeamData existing = teamScores.Find(t => t.teamName == teamName);
        if (existing != null)
        {
            existing.winpoints += winPoints;
        }
        else
        {
            teamScores.Add(new TeamData { teamName = teamName, winpoints = winPoints });
        }
        SaveScores();
        pcServer.SendToTablet(GetTeamDataMessage());
    }

    public void SaveScores()
    {
        string json = JsonUtility.ToJson(new TeamDataListWrapper { teamScores = teamScores }, true);
        File.WriteAllText(filePath, json);
    }

    public void LoadScores()
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            TeamDataListWrapper wrapper = JsonUtility.FromJson<TeamDataListWrapper>(json);
            teamScores = wrapper.teamScores ?? new List<TeamData>();
        }
    }

    public void ClearScores()
    {
        // Clear the in-memory list
        teamScores.Clear();
        // Delete the file if it exists
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log("[Leaderboard] JSON file deleted.");
        }
        else
        {
            Debug.Log("[Leaderboard] No file found to delete.");
        }
    }

}
#endif