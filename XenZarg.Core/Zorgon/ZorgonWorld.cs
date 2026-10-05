using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Input;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;

namespace XenZarg.Core.Zorgon;

/// <summary>A horizontal surface the hero can stand on. Deadly surfaces (the river) kill on landing.</summary>
public readonly record struct Surface(int X0, int X1, int Top, bool Deadly = false)
{
    public bool Contains(float x) => x >= X0 && x < X1 + 1;
}

/// <summary>State and helpers shared by every Zorgon's Revenge screen.</summary>
public sealed class ZorgonWorld
{
    /// <summary>
    /// The original ran each screen's loop flat out; speeds here are the original per-loop
    /// rates scaled by one common factor so every object keeps its relative speed.
    /// </summary>
    public const float Speed = 2.0f;

    public const int HudTop = 200;
    public const int Bottom = 199;

    public ZorgonWorld(GameSession session, IControls controls, ISoundPlayer sound, Random rng)
    {
        Session = session;
        Controls = controls;
        Sound = sound ?? SilentSoundPlayer.Instance;
        Rng = rng ?? new Random();
    }

    public GameSession Session { get; }
    public IControls Controls { get; }
    public ISoundPlayer Sound { get; }
    public Random Rng { get; }
    public int Tick { get; set; }

    /// <summary>Displayed bonus (the original stored it /10 and printed a trailing 0).</summary>
    public int Bonus { get; set; }

    /// <summary>0 at the easiest setting, rising with skill and completed cycles.</summary>
    public int Hardness => Math.Max(0, Session.Difficulty - 1);

    /// <summary>Converts a rate in pixels (or rows) per second of the original into a per-tick step.</summary>
    public static float PerTick(float perSecond) => perSecond * Speed / XenZargGame.TicksPerSecond;

    public bool Chance(double perTick) => Rng.NextDouble() < perTick;

    /// <summary>The bonus drops by 50 about once a second while a mission runs, never below 1000.</summary>
    public void CountDownBonus()
    {
        if (Tick % XenZargGame.TicksPerSecond == 0 && Bonus > 1000)
            Bonus = Math.Max(1000, Bonus - 50);
    }

    /// <summary>Zorgon's Revenge status lines; paper and ink differ per screen as on the Oric.</summary>
    public void DrawHud(Canvas c, OricColor paper, OricColor ink)
    {
        c.Fill(0, HudTop, Canvas.Width, Canvas.Height - HudTop, paper);
        c.TextAt(2, 200, "SCORE", ink);
        c.TextAt(17, 200, "MEN", ink);
        c.TextAt(31, 200, "HI-SCORE", ink);
        c.TextAt(2, 208, Session.Score.ToString("D6"), ink);
        c.TextAt(18, 208, Session.Bases.ToString(), ink);
        c.TextAt(32, 208, Session.HiScore.ToString("D6"), ink);
        c.TextAt(9, 216, "SKILL " + Session.Skill, ink);
        c.TextAt(21, 216, "BONUS " + Bonus, ink);
    }
}
