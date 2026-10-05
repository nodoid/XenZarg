using System;

namespace XenZarg.Core.Session;

/// <summary>Which of the two original games the player is currently in.</summary>
public enum Episode
{
    Xenon,
    Zorgon,
}

/// <summary>State shared across Xenon 1 and Zorgon's Revenge for one play-through.</summary>
public sealed class GameSession
{
    public const int StartingBases = 3;
    public const int MaxSkill = 9;
    public const int MaxBases = 9;

    /// <summary>A free base is awarded at each multiple of this score.</summary>
    public const int ExtraBaseEvery = 10000;

    public GameSession(int skill, int hiScore)
    {
        Skill = Math.Clamp(skill, 1, MaxSkill);
        HiScore = hiScore;
        Bases = StartingBases;
    }

    public int Score { get; private set; }
    public int HiScore { get; private set; }
    public int Bases { get; private set; }
    public int Skill { get; }

    /// <summary>Number of times both games have been completed (raises difficulty).</summary>
    public int Cycle { get; private set; }

    public Episode Episode { get; set; } = Episode.Xenon;

    public bool IsGameOver => Bases <= 0;

    /// <summary>Difficulty 1..n combining chosen skill and completed cycles.</summary>
    public int Difficulty => Skill + Cycle * 2;

    public event Action ExtraBaseAwarded;

    public void AddScore(int points)
    {
        if (points <= 0)
            return;
        int before = Score / ExtraBaseEvery;
        Score = Math.Min(999999, Score + points);
        if (Score / ExtraBaseEvery > before && Bases < MaxBases)
        {
            Bases++;
            ExtraBaseAwarded?.Invoke();
        }
        if (Score > HiScore)
            HiScore = Score;
    }

    public void LoseBase()
    {
        if (Bases > 0)
            Bases--;
    }

    public void CompleteCycle()
    {
        Cycle++;
        Episode = Episode.Xenon;
    }
}
