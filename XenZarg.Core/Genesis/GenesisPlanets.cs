using System;
using System.Collections.Generic;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Genesis;

/// <summary>The six planets of Xenon 3 - The Genesis Probe, in the order of the original's chart.</summary>
public enum Planet
{
    Rundus,
    Xer,
    Somtana,
    Nuciro,
    Terra,
    Radon,
}

public enum MissionKind
{
    Capture,
    Worms,
    Final,
}

/// <summary>One creature worth two places in the Genesis Probe's hold.</summary>
public sealed record SpeciesDef(string Name, string[] Sprite, OricColor Colour);

public static class GenesisPlanets
{
    public const int PlanetCount = 6;

    public static readonly Planet[] All =
        [Planet.Rundus, Planet.Xer, Planet.Somtana, Planet.Nuciro, Planet.Terra, Planet.Radon];

    public static string Name(Planet p) => p switch
    {
        Planet.Rundus => "RUNDUS",
        Planet.Xer => "XER",
        Planet.Somtana => "SOMTANA",
        Planet.Nuciro => "NUCIRO",
        Planet.Terra => "TERRA",
        _ => "RADON",
    };

    public static OricColor Colour(Planet p) => p switch
    {
        Planet.Rundus => OricColor.Green,
        Planet.Xer => OricColor.Red,
        Planet.Somtana => OricColor.Blue,
        Planet.Nuciro => OricColor.Cyan,
        Planet.Terra => OricColor.Red,
        _ => OricColor.Magenta,
    };

    public static MissionKind Kind(Planet p) => p switch
    {
        Planet.Xer => MissionKind.Worms,
        Planet.Radon => MissionKind.Final,
        _ => MissionKind.Capture,
    };

    /// <summary>The ten species of Rundus.</summary>
    public static readonly SpeciesDef[] RundusSpecies =
    [
        new("FISH", SpriteData.GSpecFish, OricColor.Cyan),
        new("BIRD", SpriteData.GSpecBird, OricColor.White),
        new("BEETLE", SpriteData.GSpecBeetle, OricColor.Magenta),
        new("BLOB", SpriteData.GSpecBlob, OricColor.Green),
        new("CRAB", SpriteData.GSpecCrab, OricColor.Red),
        new("SNAKE", SpriteData.GSpecSnake, OricColor.Yellow),
        new("SPIDER", SpriteData.GSpecSpider, OricColor.Blue),
        new("HOPPER", SpriteData.GSpecHopper, OricColor.Green),
        new("CRYSTAL", SpriteData.GSpecCrystal, OricColor.Cyan),
        new("WORM", SpriteData.GSpecWorm, OricColor.Magenta),
    ];

    /// <summary>Terra's hardier fauna.</summary>
    public static readonly SpeciesDef[] TerraSpecies =
    [
        new("FISH", SpriteData.GSpecFish, OricColor.Cyan),
        new("BEETLE", SpriteData.GSpecBeetle, OricColor.Magenta),
        new("CRAB", SpriteData.GSpecCrab, OricColor.Red),
        new("SNAKE", SpriteData.GSpecSnake, OricColor.Yellow),
        new("HOPPER", SpriteData.GSpecHopper, OricColor.Green),
        new("CRYSTAL", SpriteData.GSpecCrystal, OricColor.Cyan),
    ];

    /// <summary>Somtana's eight species.</summary>
    public static readonly SpeciesDef[] SomtanaSpecies =
        [RundusSpecies[0], RundusSpecies[1], RundusSpecies[2], RundusSpecies[3],
         RundusSpecies[4], RundusSpecies[5], RundusSpecies[6], RundusSpecies[7]];

    /// <summary>Nuciro's six species.</summary>
    public static readonly SpeciesDef[] NuciroSpecies =
        [RundusSpecies[0], RundusSpecies[2], RundusSpecies[3], RundusSpecies[4],
         RundusSpecies[7], RundusSpecies[8]];

    public static SpeciesDef[] Species(Planet p) => p switch
    {
        Planet.Terra => TerraSpecies,
        Planet.Somtana => SomtanaSpecies,
        Planet.Nuciro => NuciroSpecies,
        _ => RundusSpecies,
    };

    /// <summary>How many of each species must be caught.</summary>
    public static int RequiredEach(Planet p) => 2;
}

/// <summary>Progress through the six planets: five must be cleared before Radon opens.</summary>
public sealed class GenesisCampaign
{
    private readonly bool[] _done = new bool[GenesisPlanets.PlanetCount];

    public bool IsDone(Planet p) => _done[(int)p];

    public bool RadonUnlocked
    {
        get
        {
            for (int i = 0; i < GenesisPlanets.PlanetCount - 1; i++)
                if (!_done[i])
                    return false;
            return true;
        }
    }

    public int ClearedCount
    {
        get
        {
            int n = 0;
            foreach (bool d in _done)
                if (d)
                    n++;
            return n;
        }
    }

    public bool CanChoose(Planet p) => p != Planet.Radon || RadonUnlocked;

    public void Complete(Planet p) => _done[(int)p] = true;

    /// <summary>The next planet that still needs a visit, or Radon once the other five are done.</summary>
    public Planet NextOpen(Planet from)
    {
        for (int i = 1; i <= GenesisPlanets.PlanetCount; i++)
        {
            var p = (Planet)(((int)from + i) % GenesisPlanets.PlanetCount);
            if (CanChoose(p) && !IsDone(p))
                return p;
        }
        return Planet.Radon;
    }
}
