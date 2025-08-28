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
    public List<MatchData> matchData = new List<MatchData>();
    private string teamDatafilePath;
    private string matchDatafilePath;
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

    public void UpdateLeaderboardDisplay()
    {
        Debug.Log("[PcUI] Updating leaderboard display...");
        CalculateStats();
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
        Debug.Log("[PcUI] Leaderboard display updated.");
    }

    void Start()
    {
        androidconnectiontext.SetActive(false);
        PcUI.SetActive(true);
        LoadScores();
        LoadMatches();
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

        teamDatafilePath = Application.persistentDataPath + "/leaderboard.json";
        matchDatafilePath = Application.persistentDataPath + "/matchdata.json";
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

    public void CalculateStats()
    {
        foreach (var team in teamScores)
        {
            int totalAllianceScore = 0;
            int totalOpponentScore = 0;
            int gamesPlayed = 0;

            // Loop through all matches and check if the team participated
            foreach (var match in matchData)
            {
                bool onRed = (match.Red1 == team.teamName || match.Red2 == team.teamName);
                bool onBlue = (match.Blue1 == team.teamName || match.Blue2 == team.teamName);

                if (onRed || onBlue)
                {
                    gamesPlayed++;

                    if (onRed)
                    {
                        totalAllianceScore += match.RedScore;
                        totalOpponentScore += match.BlueScore;
                    }
                    else if (onBlue)
                    {
                        totalAllianceScore += match.BlueScore;
                        totalOpponentScore += match.RedScore;
                    }
                }
            }

            if (gamesPlayed > 0)
            {
                team.avgAllianceScore = (float)totalAllianceScore / gamesPlayed;
                team.avgOpponentScore = (float)totalOpponentScore / gamesPlayed;
                team.avgScoreDiff = team.avgAllianceScore - team.avgOpponentScore;
                team.approxContribution = team.avgAllianceScore / 2f; // since 2 teams per alliance
            }
            else
            {
                team.avgAllianceScore = 0;
                team.avgOpponentScore = 0;
                team.avgScoreDiff = 0;
                team.approxContribution = 0;
            }

            // 🔹 Strength of Schedule (average win rate of opponents)
            int opponentMatches = 0;
            float opponentWins = 0;

            foreach (var match in matchData)
            {
                bool onRed = (match.Red1 == team.teamName || match.Red2 == team.teamName);
                bool onBlue = (match.Blue1 == team.teamName || match.Blue2 == team.teamName);

                if (onRed || onBlue)
                {
                    List<string> opponents = onRed
                        ? new List<string> { match.Blue1, match.Blue2 }
                        : new List<string> { match.Red1, match.Red2 };

                    foreach (string opp in opponents)
                    {
                        var oppTeam = teamScores.FirstOrDefault(t => t.teamName == opp);
                        if (oppTeam != null && oppTeam.matchesPlayed > 0)
                        {
                            opponentWins += (float)oppTeam.matchesWon / oppTeam.matchesPlayed;
                            opponentMatches++;
                        }
                    }
                }
            }

            team.sos = (opponentMatches > 0) ? opponentWins / opponentMatches : 0;
        }
    }

    public void SaveScores()
    {
        string json = JsonUtility.ToJson(new TeamDataListWrapper { teamScores = teamScores }, true);
        File.WriteAllText(teamDatafilePath, json);
        pcServer.SendToTablet(GetTeamDataMessage());
    }

    public void LoadScores()
    {
        if (File.Exists(teamDatafilePath))
        {
            string json = File.ReadAllText(teamDatafilePath);
            TeamDataListWrapper wrapper = JsonUtility.FromJson<TeamDataListWrapper>(json);
            teamScores = wrapper.teamScores ?? new List<TeamData>();
        }
    }

    public void SaveMatches()
    {
        string json = JsonUtility.ToJson(new MatchDataListWrapper { matchData = matchData }, true);
        File.WriteAllText(matchDatafilePath, json);
    }

    public void LoadMatches()
    {
        if (File.Exists(matchDatafilePath))
        {
            string json = File.ReadAllText(matchDatafilePath);
            MatchDataListWrapper wrapper = JsonUtility.FromJson<MatchDataListWrapper>(json);
            matchData = wrapper.matchData ?? new List<MatchData>();
        }
    }

    public void ClearScores()
    {
        // Clear the in-memory list
        teamScores.Clear();
        // Delete the file if it exists
        if (File.Exists(teamDatafilePath))
        {
            File.Delete(teamDatafilePath);
            Debug.Log("[Leaderboard] JSON file deleted.");
        }
        else
        {
            Debug.Log("[Leaderboard] No file found to delete.");
        }
    }

}
#endif