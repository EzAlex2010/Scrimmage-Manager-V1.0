#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Security.Cryptography;
public class AndroidUI : MonoBehaviour
{
    public TabletClient tabletClient; // Reference to your TabletClient script
    public List<TeamData> teamScores = new List<TeamData>();
    public int numOfTeamsSelected = 0;
    public int maxSelectedTeams = 4; // Max teams allowed in a match
    public bool recordScores = false;
    public string Red1;
    public string Red2;
    public string Blue1;
    public string Blue2;
    public bool runAuton = true; // Flag to control auton mode. True by default
    public string passcode;

    [Header("UI References")]
    public GameObject CreateMatchButton; // Button to create a match
    public GameObject ViewLeaderboardButton; // Button to create a match

    [Header("Team Selection Panel")]
    public Transform teamListContent; // The Scroll View Content object
    public Transform selectedTeamContent; // The other scroll list
    public GameObject teamButtonPrefab; // The button prefab
    public GameObject TeamSelectPanel; // Panel to show team selection UI

    [Header("Match Settings Panel")]
    public GameObject MatchSettingsPanel; // New panel to show selected teams
    public Transform selectedTeamsPanelContent; // Content inside the new panel
    public GameObject teamRowPrefab; // New prefab for rows with Red/Blue buttons
    public Button nextButton; // Button to go to next step in match creation
    public TMP_InputField recordscorespass;

    [Header("Run Match Panel")]
    public GameObject RunMatchPanel; // Panel to run the match
    public TMP_Text TeamRed1;
    public TMP_Text TeamRed2;
    public TMP_Text TeamBlue1;
    public TMP_Text TeamBlue2;

    [Header("Control Panel")]
    public GameObject ControlPanel; // Panel to control the match
    public Button StartMatchButton;
    public Button EndEarlyButton;
    public Button AbortMatchButton;
    public TMP_Text MatchStateText;
    public TMP_Text MatchTimeText;

    [Header("Scoring Panel")]
    public GameObject ScoringPanel; // Panel to score the match

    [Header("Leaderboard Panel")]
    public GameObject LeaderboardPanel; // Panel to view the leaderboard
    public Transform scrollContent;      // Reference to the ScrollView Content object
    public GameObject teamDataPrefab;    // Reference to your TeamData_UI prefab

    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            CreateMatchButton.SetActive(true);
            ViewLeaderboardButton.SetActive(true);
        }
    }

    public void SaveTeamData(List<TeamData> newTeams)
    {
        teamScores = newTeams ?? new List<TeamData>();
        Debug.Log($"[AndroidUI] Loaded {teamScores.Count} teams from JSON");
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

    public void OpenMatchCreation()
    {
        TeamSelectPanel.SetActive(true);
        CreateMatchButton.SetActive(false);
        ViewLeaderboardButton.SetActive(false);
        PopulateTeamButtons();
    }

    public void OpenLeaderboard()
    {
        LeaderboardPanel.SetActive(true);
        CreateMatchButton.SetActive(false);
        ViewLeaderboardButton.SetActive(false);
        UpdateLeaderboardDisplay();
    }

    public void CloseMatchCreation()
    {
        TeamSelectPanel.SetActive(false);
        CreateMatchButton.SetActive(true);
        ViewLeaderboardButton.SetActive(true);
    }

    public void CloseLeaderboard()
    {
        LeaderboardPanel.SetActive(false);
        CreateMatchButton.SetActive(true);
        ViewLeaderboardButton.SetActive(true);
    }

    public void SetRunAuton(bool value)
    {
        runAuton = value;
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
        RunMatchPanel.SetActive(true);
        ControlPanel.SetActive(true);
        ScoringPanel.SetActive(false);
        TeamRed1.text = Red1 ?? "N/A";
        TeamRed2.text = Red2 ?? "N/A";
        TeamBlue1.text = Blue1 ?? "N/A";
        TeamBlue2.text = Blue2 ?? "N/A";
        tabletClient.SendCommand($"CreateMatch|Red1:{Red1}|Red2:{Red2}|Blue1:{Blue1}|Blue2:{Blue2}|Auton:{runAuton}|Record:{recordScores}");
    }

    public void EndMatch()
    {
        tabletClient.SendCommand("EndMatch");
        //Open Scoring Panel
        ControlPanel.SetActive(false);
        ScoringPanel.SetActive(true);
    }

    public void StartMatch()
    {
        tabletClient.SendCommand("StartMatch");
    }

    public void EndMatchEarly()
    {
        tabletClient.SendCommand("EndMatchEarly");
    }

    public void UpdateMatchState(string state)
    {
        MatchStateText.text = state;
    }

    public void UpdateMatchTime(string time)
    {
        MatchTimeText.text = time;
    }

    public void SetButtonInteractable(string buttonName, bool interactable)
    {
        switch (buttonName)
        {
            case "Start":
                StartMatchButton.interactable = interactable;
                break;
            case "EndEarly":
                EndEarlyButton.interactable = interactable;
                break;
            case "Abort":
                AbortMatchButton.interactable = interactable;
                break;
            default:
                Debug.LogWarning($"Unknown button name: {buttonName}");
                break;
        }
    }
}

#endif