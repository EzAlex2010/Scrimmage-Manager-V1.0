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

    [Header("UI References")]
    public TextMeshProUGUI leaderboardText;

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
        UpdateLeaderboardDisplay();
    }

    public void UpdateLeaderboardDisplay()
    {
        leaderboardText.text = "";
        foreach (var team in teamScores)
        {
            leaderboardText.text += $"{team.teamName}: {team.winpoints}\n";
        }
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
        LoadScores();
        UpdateLeaderboardDisplay();
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
        teamScores.Sort((a, b) => b.winpoints.CompareTo(a.winpoints));
        SaveScores();
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