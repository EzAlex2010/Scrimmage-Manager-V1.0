using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI;
using UnityEditor.UI;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;
    public List<TeamData> teamScores = new List<TeamData>();
    public int maxSelectedTeams = 4;
    private string filePath;
    public int numOfTeamsSelected = 0;
    public bool recordScores = false;
    public string passcode = "6741 Robotics";

    public string Red1;
    public string Red2;
    public string Blue1;
    public string Blue2;

    public bool runAuton = true; // Flag to control auton mode. True by default

    [Header("UI References")]
    public TextMeshProUGUI leaderboardText;
    [Header("Team Selection Panel")]
    public Transform teamListContent; // The Scroll View Content object
    public Transform selectedTeamContent; // The other scroll list
    public GameObject teamButtonPrefab; // The button prefab
    public GameObject CreateMatchButton; // Button to create a match
    public GameObject TeamSelectPanel; // Panel to show team selection UI
    [Header("Match Settings Panel")]
    public GameObject MatchSettingsPanel; // New panel to show selected teams
    public Transform selectedTeamsPanelContent; // Content inside the new panel
    public GameObject teamRowPrefab; // New prefab for rows with Red/Blue buttons
    public Button nextButton; // Button to go to next step in match creation
    public TMP_InputField recordscorespass;


    void Start()
    {

    }

    public void OpenMatchCreation()
    {
        TeamSelectPanel.SetActive(true);
        CreateMatchButton.SetActive(false);
        PopulateTeamButtons();
    }

    public void CloseMatchCreation()
    {
        TeamSelectPanel.SetActive(false);
        CreateMatchButton.SetActive(true);
    }

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

    public void SetRunAuton(bool value)
    {
        runAuton = value;
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

    public void PopulateTeamButtons()
    {
        foreach (Transform child in teamListContent)
            Destroy(child.gameObject);
        foreach (Transform child in selectedTeamContent)
            Destroy(child.gameObject);
        foreach (var team in teamScores)
        {
            GameObject buttonObj = Instantiate(teamButtonPrefab, teamListContent);
            buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = team.teamName;
            Button btn = buttonObj.GetComponent<Button>();
            // Remove previous listeners to be safe
            btn.onClick.RemoveAllListeners();
            // Add a listener that calls OnTeamButtonClicked with this button
            btn.onClick.AddListener(() => OnTeamButtonClicked(buttonObj));
        }
    }

    private void OnTeamButtonClicked(GameObject buttonObj)
    {
        Transform currentParent = buttonObj.transform.parent;
        if (currentParent == teamListContent)
        {
            if (selectedTeamContent.childCount < maxSelectedTeams)
                buttonObj.transform.SetParent(selectedTeamContent, false);
            else
                Debug.Log("Selected list full!");
        }
        else
        {
            buttonObj.transform.SetParent(teamListContent, false);
        }
    }

    public void OpenMatchSettingsPanel()
    {
        // Close the team selection panel
        TeamSelectPanel.SetActive(false);
        numOfTeamsSelected = 0;
        nextButton.interactable = false; // Disable next button until teams are selected
        Red1 = null;
        Red2 = null;
        Blue1 = null;
        Blue2 = null;
        // Clear the panel content
        foreach (Transform child in selectedTeamsPanelContent) Destroy(child.gameObject);
        // Populate panel with selected teams
        foreach (Transform teamButton in selectedTeamContent)
        {
            string teamName = teamButton.GetComponentInChildren<TextMeshProUGUI>().text;
            TeamData team = teamScores.Find(t => t.teamName == teamName);

            GameObject rowObj = Instantiate(teamRowPrefab, selectedTeamsPanelContent);
            TeamRowUI rowUI = rowObj.GetComponent<TeamRowUI>();
            rowUI.Setup(team, (t, alliance) => AssignToAlliance(t.teamName, alliance, rowUI));
            numOfTeamsSelected++;
        }
        MatchSettingsPanel.SetActive(true);
    }

    public void UpdateNextButtonState()
    {
        nextButton.interactable = numOfTeamsSelected == 0; // Enable next button only if all teams are assigned to alliances
    }

    private void AssignToAlliance(string teamNumber, string alliance, TeamRowUI rowUI)
    {
        // Assign to the first free slot in the selected alliance
        if (alliance == "Red")
        {
            if (Blue1 == teamNumber) Blue1 = null;
            if (Blue2 == teamNumber) Blue2 = null;
            if (Red1 == teamNumber || Red2 == teamNumber)
            {
                Debug.Log("Team already assigned to Red alliance!");
                rowUI.teamNameText.color = Color.white; // change color
                if (Red1 == teamNumber) Red1 = null;
                if (Red2 == teamNumber) Red2 = null;
                numOfTeamsSelected++;
                UpdateNextButtonState();
                return;
            }
            else if (string.IsNullOrEmpty(Red1)) Red1 = teamNumber;
            else if (string.IsNullOrEmpty(Red2)) Red2 = teamNumber;
            else
            {
                Debug.Log("Red alliance is full!");
                rowUI.teamNameText.color = Color.white; // change color
                numOfTeamsSelected++;
                UpdateNextButtonState();
                return;
            }
            numOfTeamsSelected--;
            rowUI.teamNameText.color = Color.red; // change color
        }
        else if (alliance == "Blue")
        {
            if (Red1 == teamNumber) Red1 = null;
            if (Red2 == teamNumber) Red2 = null;
            if (Blue1 == teamNumber || Blue2 == teamNumber)
            {
                Debug.Log("Team already assigned to Blue alliance!");
                rowUI.teamNameText.color = Color.white; // change color
                if (Blue1 == teamNumber) Blue1 = null;
                if (Blue2 == teamNumber) Blue2 = null;
                numOfTeamsSelected++;
                UpdateNextButtonState();
                return;
            }
            else if (string.IsNullOrEmpty(Blue1)) Blue1 = teamNumber;
            else if (string.IsNullOrEmpty(Blue2)) Blue2 = teamNumber;
            else
            {
                Debug.Log("Blue alliance is full!");
                rowUI.teamNameText.color = Color.white; // change color
                numOfTeamsSelected++;
                UpdateNextButtonState();
                return;
            }
            numOfTeamsSelected--;
            rowUI.teamNameText.color = Color.blue; // change color
        }
        UpdateNextButtonState();
        Debug.Log($"Assigned {teamNumber} to {alliance}");
    }

    public void CloseMatchSettingsPanel()
    {
        MatchSettingsPanel.SetActive(false);
        TeamSelectPanel.SetActive(true);
        Red1 = null;
        Red2 = null;
        Blue1 = null;
        Blue2 = null;
    }

    public void CreateMatchAndSet()
    {
        if (recordscorespass.text == passcode)
        {
            recordScores = true;
            Debug.Log("Passcode correct! recordScores = true");
        }
        else
        {
            recordScores = false;
            Debug.Log("Passcode incorrect. recordScores = false");
            if (recordscorespass.text != "") return;
        }
        Debug.Log($"Creating match with Red1: {Red1}, Red2: {Red2}, Blue1: {Blue1}, Blue2: {Blue2}");
        MatchSettingsPanel.SetActive(false);
    }
}
