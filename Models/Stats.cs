namespace EasyFortniteStats_ImageApi.Models;

public class Stats
{
    public string PlayerName { get; set; }
    public string InputType { get; set; }
    public bool IsVerified { get; set; }
    public string? UserName { get; set; }
    public string? BackgroundImagePath { get; set; }
    public Playtime Playtime { get; set; }
    public float BattlePassLevel { get; set; }
    public string[] BattlePassLevelBarColors { get; set; }
    public ArenaStats? Arena { get; set; }
    public StatsEntry Overall { get; set; }
    public StatsEntry Solo { get; set; }
    public StatsEntry Duos { get; set; }
    public StatsEntry Trios { get; set; }
    public StatsEntry Squads { get; set; }
    public StatsEntry? Teams { get; set; }
}

public class ArenaStats
{
    public string? HypePoints { get; set; }
    public string Division { get; set; }
    public string League { get; set; }
    public string Earnings { get; set; }
    public string PowerRanking { get; set; }
}

public class StatsEntry
{
    public string MatchesPlayed { get; set; }
    public string Wins { get; set; }
    public string WinRatio { get; set; }
    public string Kills { get; set; }
    public string KD { get; set; }
    public string? Top25 { get; set; }
    public string? Top12 { get; set; }
    public string? Top6 { get; set; }
}

public class Playtime
{
    public string Days { get; set; }
    public string Hours { get; set; }
    public string Minutes { get; set; }
}