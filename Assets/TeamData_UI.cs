using UnityEngine;
using TMPro;

public class TeamData_UI : MonoBehaviour
{
    public TMP_Text teamNumberText;
    public TMP_Text winPointsText;
    public TMP_Text MatchDataText;
    public TMP_Text WinPercentText;
    public TMP_Text highScoreText;
    public TMP_Text avgScoreText;
    public TMP_Text avgOppScoreText;
    public TMP_Text avgScoreDiffText;
    public TMP_Text ContributionsText;
    public TMP_Text SosText;

    [HideInInspector] public TeamData teamData;

    public void Setup(TeamData team)
    {
        teamData = team;
        teamNumberText.text = team.teamName.ToString();
        winPointsText.text = team.winpoints.ToString();
        MatchDataText.text = (team.matchesWon + "/" + team.matchesPlayed).ToString();
        WinPercentText.text = team.matchesPlayed > 0 ? ((float)team.matchesWon / team.matchesPlayed * 100).ToString("F1") + "%" : "0%";
        highScoreText.text = team.highScore.ToString();
        avgScoreText.text = team.avgAllianceScore.ToString("F1");
        avgOppScoreText.text = team.avgOpponentScore.ToString("F1");
        avgScoreDiffText.text = team.avgScoreDiff.ToString("F1");
        ContributionsText.text = team.approxContribution.ToString("F1");
        SosText.text = team.sos.ToString("P1"); // percent format
        Debug.Log("[TeamData_UI] Set up UI for team: " + team.teamName);
    }
}
