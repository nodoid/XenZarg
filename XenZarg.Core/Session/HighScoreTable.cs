using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XenZarg.Core.Session;

public sealed record HighScoreEntry(int Score, string Name);

/// <summary>The ten-place "HALL OF FAME", persisted as plain text.</summary>
public sealed class HighScoreTable
{
    public const int Capacity = 10;
    public const int NameLength = 5;
    private const string FileName = "halloffame.txt";

    private readonly List<HighScoreEntry> _entries = new();
    private readonly string _directory;

    public HighScoreTable(string directory)
    {
        _directory = directory;
        Reset();
    }

    public IReadOnlyList<HighScoreEntry> Entries => _entries;

    public int Best => _entries.Count > 0 ? _entries[0].Score : 0;

    /// <summary>Fills the table with the default scores.</summary>
    public void Reset()
    {
        _entries.Clear();
        for (int i = 0; i < Capacity; i++)
            _entries.Add(new HighScoreEntry(1000 - i * 100, "PFJ"));
    }

    public bool Qualifies(int score) => score > 0 && (_entries.Count < Capacity || score > _entries[^1].Score);

    /// <summary>Inserts a score, returning its rank (0 based) or -1 if it did not qualify.</summary>
    public int Add(int score, string name)
    {
        if (!Qualifies(score))
            return -1;
        name = Sanitize(name);
        int index = _entries.FindIndex(e => score > e.Score);
        if (index < 0)
            index = _entries.Count;
        _entries.Insert(index, new HighScoreEntry(score, name));
        while (_entries.Count > Capacity)
            _entries.RemoveAt(_entries.Count - 1);
        return index;
    }

    public static string Sanitize(string name)
    {
        var chars = (name ?? string.Empty).ToUpperInvariant()
            .Where(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '.')
            .Take(NameLength)
            .ToArray();
        var s = new string(chars).Trim();
        return s.Length == 0 ? "?????" : s;
    }

    public string Serialize() =>
        string.Join("\n", _entries.Select(e => $"{e.Score}\t{e.Name}"));

    public void Deserialize(string text)
    {
        var parsed = new List<HighScoreEntry>();
        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length == 2 && int.TryParse(parts[0], out int score) && score >= 0)
                parsed.Add(new HighScoreEntry(Math.Min(score, 999999), Sanitize(parts[1])));
        }
        if (parsed.Count == 0)
            return;
        _entries.Clear();
        _entries.AddRange(parsed.OrderByDescending(e => e.Score).Take(Capacity));
    }

    public void Load()
    {
        try
        {
            string path = Path.Combine(_directory, FileName);
            if (File.Exists(path))
                Deserialize(File.ReadAllText(path));
        }
        catch (Exception)
        {
            // A corrupt or unreadable table falls back to defaults.
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, FileName), Serialize());
        }
        catch (Exception)
        {
        }
    }
}
