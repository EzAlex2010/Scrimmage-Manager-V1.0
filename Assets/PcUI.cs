#if UNITY_STANDALONE || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI;
using System.Linq;
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;
    public List<TeamData> teamScores = new List<TeamData>();
    private string filePath;
    public string passcode;

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
    public TMP_InputField teamnumberInput;
    public TMP_Text teammodifytext;

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
        string passcodeFilePath = Application.persistentDataPath + "/passcode.txt";
        if (File.Exists(passcodeFilePath))
        {
            passcode = File.ReadAllText(passcodeFilePath).Trim();
            Debug.Log("[Leaderboard] Passcode loaded: " + passcode);
        }
        else
        {
            Debug.LogWarning("[Leaderboard] Passcode file not found at: " + passcodeFilePath);
            File.WriteAllText(passcodeFilePath, "");
            passcode = "";
            Debug.Log("[Leaderboard] Blank passcode file created at: " + passcodeFilePath);
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
    }

    public void Addteam()
    {
        string teamName = teamnumberInput.text;
        if (string.IsNullOrWhiteSpace(teamName))
        {
            teammodifytext.text = "Enter a Team Number!";
            return;
        }
        if (teamScores.Any(t => t.teamName == teamName))
        {
            teammodifytext.text = "Team Already Exists!";
            teamnumberInput.text = "";
            return;
        }
        var newTeam = new TeamData { teamName = teamName };
        teamScores.Add(newTeam);
        SaveScores();
        UpdateLeaderboardDisplay();
        teamnumberInput.text = "";
        teammodifytext.text = "Team Added!";
    }

    public void RemoveTeam()
    {
        var teamToRemove = teamScores.FirstOrDefault(t => t.teamName == teamnumberInput.text);
        if (teamToRemove != null)
        {
            teamScores.Remove(teamToRemove);
            SaveScores();
            UpdateLeaderboardDisplay();
            teammodifytext.text = "Team Removed!";
        }
        else
        {
            Debug.Log("Team not found: " + teamnumberInput.text);
            teammodifytext.text = "Team Not Found!";
        }
        teamnumberInput.text = "";
    }

    public string GetTeamDataMessage()
    {
        // Wrap the teamScores list in your existing wrapper so it serializes cleanly
        string json = JsonUtility.ToJson(new TeamDataListWrapper { teamScores = teamScores });
        Debug.Log("Serialized team data JSON: " + json);
        return "TeamData:" + json;
    }

    public void AddWinPoints(string teamName, int pointsAwarded, int scoreThisMatch = 0, bool won = false)
    {
        var team = teamScores.FirstOrDefault(t => t.teamName == teamName);
        if (team == null)
        {
            team = new TeamData { teamName = teamName };
            teamScores.Add(team);
        }
        // Always count participation
        team.matchesPlayed++;
        // Only increment if the team actually won
        if (won)
            team.matchesWon++;
        // Award WP (0, 1, or 2 depending on win/tie/loss)
        team.winpoints += pointsAwarded;
        // Track highest score
        if (scoreThisMatch > team.highScore)
            team.highScore = scoreThisMatch;

        SaveScores();
        UpdateLeaderboardDisplay();
    }

    public void SaveScores()
    {
        string json = JsonUtility.ToJson(new TeamDataListWrapper { teamScores = teamScores }, true);
        File.WriteAllText(filePath, json);
        pcServer.SendToTablet(GetTeamDataMessage());
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