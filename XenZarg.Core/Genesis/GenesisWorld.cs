using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Input;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;

namespace XenZarg.Core.Genesis;

/// <summary>State shared by every Xenon 3 screen.</summary>
public sealed class GenesisWorld
{
    public const int HudTop = 200;
    public const int PlayTop = 0;

    /// <summary>The action runs the full height of the screen, under the status panel.</summary>
    public const int PlayBottom = 223;

    public GenesisWorld(GameSession session, IControls controls, ISoundPlayer sound, Random rng)
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

    /// <summary>0 at the easiest setting, rising with skill and completed cycles.</summary>
    public int Hardness => Math.Max(0, Session.Difficulty - 1);

    public bool Chance(double perTick) => Rng.NextDouble() < perTick;

    public void AddScore(int points) => Session.AddScore(points);

    /// <summary>Takes one shield. Returns false when that was the last one.</summary>
    public bool LoseShield()
    {
        Session.LoseBase();
        return !Session.IsGameOver;
    }

    /// <summary>Ticks the probe is blown apart before it flies back into view.</summary>
    public const int ShipDownTicks = 24;

    /// <summary>Ticks the explosion bursts for.</summary>
    public const int BlastTicks = 30;

    private int _blastTick = -1000;
    private int _blastX;
    private int _blastY;

    /// <summary>Ticks since the probe last exploded.</summary>
    public int BlastAge => Tick - _blastTick;

    /// <summary>Blows the probe up, so losing a shield is something the player sees.</summary>
    public void Blast(int x, int y)
    {
        _blastTick = Tick;
        _blastX = x;
        _blastY = y;
    }

    /// <summary>True while the probe is destroyed and while it blinks back into view.</summary>
    public bool ShipHidden => BlastAge < ShipDownTicks || (BlastAge < 72 && BlastAge / 6 % 2 == 0);

    /// <summary>Draws the probe, its explosion, and its blink back into view.</summary>
    public void DrawProbe(Canvas c, GenesisShip ship)
    {
        if (!ShipHidden)
            ship.Draw(c);
        if (BlastAge >= 0 && BlastAge < BlastTicks)
        {
            var star = Sprite.Of(SpriteData.Explosion);
            var ink = (OricColor)(1 + BlastAge / 3 % 7);
            c.DrawTinted(star, _blastX - 5, _blastY - 6, ink);
        }
    }

    /// <summary>
    /// The status box: a yellow line across the playfield, then the cyan-on-black
    /// SCORE / SPECIES / SHIELDS / HI-SCORE panel of the original.
    /// </summary>
    public void DrawHud(Canvas c, int specimens)
    {
        c.Fill(0, 200, Canvas.Width, 3, OricColor.Yellow);
        c.Fill(1, 203, Canvas.Width - 2, Canvas.Height - 203, OricColor.Cyan);
        c.Fill(2, 204, Canvas.Width - 4, Canvas.Height - 206, OricColor.Black);

        c.TextAt(2, 205, "SCORE", OricColor.Cyan);
        c.TextAt(14, 205, "SPECIES", OricColor.Cyan);
        c.TextAt(23, 205, "SHIELDS", OricColor.Cyan);
        c.TextAt(31, 205, "HI-SCORE", OricColor.Cyan);

        c.TextAt(2, 213, Session.Score.ToString("D6"), OricColor.Cyan);
        c.TextAt(16, 213, Math.Clamp(specimens, 0, 99).ToString("D2"), OricColor.Cyan);
        c.TextAt(25, 213, Session.Bases.ToString(), OricColor.Cyan);
        c.TextAt(31, 213, Session.HiScore.ToString("D6"), OricColor.Cyan);
    }
}

/// <summary>Scan-line waves of a planet, in the two-colour banding of the Oric ULA.</summary>
public static class GenesisArt
{
    /// <summary>The five platform rows of the original's planet screens.</summary>
    public static readonly int[] LaneRows = [38, 72, 106, 140, 168];

    public static void DrawWaves(Canvas c, OricColor ink, OricColor shade, int tick)
    {
        foreach (int baseY in LaneRows)
        {
            // Three broken platforms per row, as on the original planet screens.
            for (int run = 0; run < 3; run++)
            {
                int x0 = run * 80 + (baseY / 34 % 2) * 8;
                int x1 = Math.Min(x0 + 70, Canvas.Width);
                for (int x = x0; x < x1; x += 4)
                {
                    int y = baseY + (int)Math.Round(Math.Sin((x + tick) * 0.1) * 2);
                    c.Fill(x, y, 4, 2, ink);
                    c.Fill(x, y + 2, 4, 1, shade);
                }
            }
        }
    }

    public static void DrawPlanetIcon(Canvas c, Planet planet, int x, int y)
    {
        // Every planet on the original's chart uses the same cyan world icon.
        c.Draw(Sprite.Of(SpriteData.GPlanet).Recolor(OricColor.Cyan), x, y);
    }
}

/// <summary>The player's Genesis Probe, flying along the foot of every action screen.</summary>
public sealed class GenesisShip
{
    public const int Top = 184;
    public const float Speed = 2.6f;

    public static Sprite Sprite => Sprite.Of(SpriteData.GShip, nameof(SpriteData.GShip));

    public float X { get; set; } = (Canvas.Width - Sprite.Width) / 2f;

    public int CentreX => (int)X + Sprite.Width / 2;

    public void Update(GenesisWorld w)
    {
        X = Math.Clamp(w.Controls.Steer(X, Speed, false, Sprite.Width / 2f), 0, Canvas.Width - Sprite.Width);
    }

    public void Draw(Canvas c) => c.Draw(Sprite, (int)X, Top);
}

/// <summary>The boomerang: thrown upwards, steered with left/right, then homing back to the ship.</summary>
public sealed class Boomerang
{
    public const float RiseSpeed = 3.4f;
    public const float SteerSpeed = 2.8f;
    public const int TopLimit = 24;

    public bool Active { get; private set; }
    public float X { get; private set; }
    public float Y { get; private set; }
    public bool Returning { get; private set; }
    public int Age { get; private set; }

    public bool TryThrow(GenesisShip ship)
    {
        if (Active)
            return false;
        Active = true;
        Returning = false;
        Age = 0;
        X = ship.CentreX - 4.5f;
        Y = GenesisShip.Top - 7;
        return true;
    }

    public void Update(GenesisWorld w, GenesisShip ship)
    {
        if (!Active)
            return;
        Age++;
        if (!Returning)
        {
            Y -= RiseSpeed;
            X = Math.Clamp(w.Controls.Steer(X, SteerSpeed, false), 0, Canvas.Width - 9);
            if (Y <= TopLimit)
                Returning = true;
        }
        else
        {
            Y += RiseSpeed;
            float target = ship.CentreX - 4.5f;
            float delta = target - X;
            X += Math.Sign(delta) * Math.Min(SteerSpeed, Math.Abs(delta));
            if (Y >= GenesisShip.Top - 2 && Math.Abs(delta) < 5)
                Active = false;
            if (Y > Canvas.Height)
                Active = false;
        }
    }

    public void Cancel()
    {
        Active = false;
        Returning = false;
    }

    public bool Hits(int x, int y, int w, int h) =>
        Active && Sprite.OverlapsRect(Frame, (int)X, (int)Y, x, y, w, h);

    public bool Hits(Sprite sprite, int x, int y) =>
        Active && Sprite.Overlaps(Frame, (int)X, (int)Y, sprite, x, y);

    public Sprite Frame => Sprite.Of((Age / 4) % 2 == 0 ? SpriteData.GBoomerang1 : SpriteData.GBoomerang2);

    public void Draw(Canvas c)
    {
        if (Active)
            c.Draw(Frame, (int)X, (int)Y);
    }
}
