using UnityEngine;
using TMPro;

public class TeamData_UI : MonoBehaviour
{
    public TMP_Text teamNumberText;
    public TMP_Text winPointsText;
    public TMP_Text MatchDataText;
    public TMP_Text WinPercentText;
    public TMP_Text highScoreText;

    [HideInInspector] public TeamData teamData;

    public void Setup(TeamData team)
    {
        teamData = team;
        teamNumberText.text = team.teamName.ToString();
        winPointsText.text = team.winpoints.ToString();
        MatchDataText.text = (team.matchesWon + "/" + team.matchesPlayed).ToString();
        WinPercentText.text = team.matchesPlayed > 0 ? ((float)team.matchesWon / team.matchesPlayed * 100).ToString("F1") + "%" : "0%";
        highScoreText.text = team.highScore.ToString();
        Debug.Log("[TeamData_UI] Set up UI for team: " + team.teamName);
    }
}
