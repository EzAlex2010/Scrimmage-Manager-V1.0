[System.Serializable]
public class TeamData
{
    public string teamName;
    public int winpoints;
    public int matchesWon;
    public int matchesPlayed;
    public int highScore;
    public int autonWinPoints;

     // Calculated stats
    [System.NonSerialized] public float avgAllianceScore;
    [System.NonSerialized] public float avgOpponentScore;
    [System.NonSerialized] public float avgScoreDiff;
    [System.NonSerialized] public float approxContribution;
    [System.NonSerialized] public float sos;
}
