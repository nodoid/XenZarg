using System.IO;
using XenZarg.Core.Session;

namespace XenZarg.Tests;

public class SessionTests
{
    [Fact]
    public void NewSessionStartsWithThreeBases()
    {
        var s = new GameSession(5, 1234);
        Assert.Equal(3, s.Bases);
        Assert.Equal(0, s.Score);
        Assert.Equal(1234, s.HiScore);
        Assert.Equal(Episode.Xenon, s.Episode);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(12, 9)]
    [InlineData(4, 4)]
    public void SkillIsClamped(int skill, int expected) => Assert.Equal(expected, new GameSession(skill, 0).Skill);

    [Fact]
    public void ScoreRaisesHiScore()
    {
        var s = new GameSession(1, 100);
        s.AddScore(150);
        Assert.Equal(150, s.HiScore);
        s.AddScore(-50);
        Assert.Equal(150, s.Score);
    }

    [Fact]
    public void ExtraBaseEveryTenThousand()
    {
        var s = new GameSession(1, 0);
        int awarded = 0;
        s.ExtraBaseAwarded += () => awarded++;
        s.AddScore(9990);
        Assert.Equal(3, s.Bases);
        s.AddScore(20);
        Assert.Equal(4, s.Bases);
        Assert.Equal(1, awarded);
    }

    [Fact]
    public void LosingAllBasesEndsTheGame()
    {
        var s = new GameSession(1, 0);
        s.LoseBase();
        s.LoseBase();
        Assert.False(s.IsGameOver);
        s.LoseBase();
        Assert.True(s.IsGameOver);
        s.LoseBase();
        Assert.Equal(0, s.Bases);
    }

    [Fact]
    public void CompletingACycleRaisesDifficultyAndReturnsToXenon()
    {
        var s = new GameSession(3, 0) { Episode = Episode.Zorgon };
        Assert.Equal(3, s.Difficulty);
        s.CompleteCycle();
        Assert.Equal(5, s.Difficulty);
        Assert.Equal(Episode.Xenon, s.Episode);
    }
}

public class HighScoreTableTests
{
    private static HighScoreTable NewTable() => new(Path.Combine(Path.GetTempPath(), "xenzarg-tests-" + System.Guid.NewGuid()));

    [Fact]
    public void DefaultsToTenDescendingEntries()
    {
        var t = NewTable();
        Assert.Equal(10, t.Entries.Count);
        for (int i = 1; i < t.Entries.Count; i++)
            Assert.True(t.Entries[i - 1].Score >= t.Entries[i].Score);
    }

    [Fact]
    public void AddInsertsInOrderAndKeepsTen()
    {
        var t = NewTable();
        int rank = t.Add(5320, "mallo");
        Assert.Equal(0, rank);
        Assert.Equal("MALLO", t.Entries[0].Name);
        Assert.Equal(10, t.Entries.Count);
        Assert.Equal(-1, t.Add(1, "LOW"));
        Assert.False(t.Qualifies(0));
    }

    [Fact]
    public void NamesAreSanitised()
    {
        Assert.Equal("ABC12", HighScoreTable.Sanitize("abc12xyz"));
        Assert.Equal("?????", HighScoreTable.Sanitize("   "));
        Assert.Equal("A.B", HighScoreTable.Sanitize("a!.b"));
    }

    [Fact]
    public void SerialisationRoundTrips()
    {
        var a = NewTable();
        a.Add(9999, "PFJ");
        var b = NewTable();
        b.Deserialize(a.Serialize());
        Assert.Equal(a.Entries, b.Entries);
    }

    [Fact]
    public void CorruptTextIsIgnored()
    {
        var t = NewTable();
        t.Deserialize("garbage\nmore\t\t");
        Assert.Equal(10, t.Entries.Count);
        Assert.Equal(1000, t.Best);
    }

    [Fact]
    public void SaveAndLoadThroughDisk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "xenzarg-tests-" + System.Guid.NewGuid());
        try
        {
            var a = new HighScoreTable(dir);
            a.Add(7777, "ROZ");
            a.Save();
            var b = new HighScoreTable(dir);
            b.Load();
            Assert.Equal(7777, b.Best);
            Assert.Equal("ROZ", b.Entries[0].Name);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
