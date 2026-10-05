using System;

namespace XenZarg.Core.Zorgon;

/// <summary>
/// Progress through Zorgon's Revenge: which mission hides behind each of the four doors
/// (shuffled each game, as the original did), which are done and how much bridge is built.
/// </summary>
public sealed class ZorgonCampaign
{
    public const int Doors = 4;
    public const int BridgePartsNeeded = 4;

    private readonly MissionKind[] _behindDoor;
    private readonly bool[] _done = new bool[Doors];
    private readonly bool[] _opened = new bool[Doors];

    public ZorgonCampaign(Random rng)
    {
        _behindDoor = [MissionKind.Spider, MissionKind.Reptile, MissionKind.Ship, MissionKind.Bird];
        for (int i = Doors - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_behindDoor[i], _behindDoor[j]) = (_behindDoor[j], _behindDoor[i]);
        }
    }

    public int PartsLaid { get; private set; }
    public bool BridgeComplete => PartsLaid >= BridgePartsNeeded;

    public MissionKind MissionBehind(int door) => _behindDoor[door];
    public bool IsDone(int door) => _done[door];
    public bool IsOpened(int door) => _opened[door];

    /// <summary>Opens a door; returns false if that mission is already complete.</summary>
    public bool Choose(int door)
    {
        if (door < 0 || door >= Doors || _done[door])
            return false;
        _opened[door] = true;
        return true;
    }

    public void MissionWon(int door) => _done[door] = true;

    public void BridgePartLaid() => PartsLaid = Math.Min(BridgePartsNeeded, PartsLaid + 1);

    /// <summary>First door not yet completed at or after <paramref name="from"/>, wrapping round.</summary>
    public int NextOpenDoor(int from, int step)
    {
        for (int i = 1; i <= Doors; i++)
        {
            int d = ((from + step * i) % Doors + Doors) % Doors;
            if (!_done[d])
                return d;
        }
        return from;
    }
}
