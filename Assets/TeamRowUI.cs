using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class TeamRowUI : MonoBehaviour
{
    public TextMeshProUGUI teamNameText;
    public Button redButton;
    public Button blueButton;
    [HideInInspector] public TeamData teamData;

    public void Setup(TeamData team, Action<TeamData, string> onAssign)
    {
        teamData = team;
        teamNameText.text = team.teamName;
        // Clear previous listeners
        redButton.onClick.RemoveAllListeners();
        blueButton.onClick.RemoveAllListeners();
        // Assign new listeners
        redButton.onClick.AddListener(() => onAssign(teamData, "Red"));
        blueButton.onClick.AddListener(() => onAssign(teamData, "Blue"));
    }
}
