using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Input;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;

namespace XenZarg.Core.Xenon;

/// <summary>A falling enemy bomb (the 4x8 zig-zag of the original).</summary>
public sealed class Bomb
{
    public float X, Y, Speed;
    public Sprite Sprite;
}

/// <summary>A short-lived white starburst, as drawn when anything is hit.</summary>
public sealed class Blast
{
    public int X, Y, Timer;
}

/// <summary>State and helpers shared by every Xenon 1 level.</summary>
public sealed class XenonWorld
{
    public const int PlayTop = 8;
    public const int PlayBottom = 199;
    public const int MaxFuel = 216;
    public const int FuelYellow = 60;
    public const int FuelRed = 30;
    public const int HudTop = 200;

    /// <summary>Ticks per unit of fuel burned (a full tank lasts about three minutes).</summary>
    public const int FuelBurnTicks = 40;

    public XenonWorld(GameSession session, IControls controls, ISoundPlayer sound, Random rng)
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

    public int Fuel { get; set; } = MaxFuel;
    public int Tick { get; set; }
    public List<Bomb> Bombs { get; } = new();
    public List<Blast> Blasts { get; } = new();

    /// <summary>The single player laser bolt allowed on screen, as in the original.</summary>
    public Laser Laser { get; } = new();

    public int Difficulty => Session.Difficulty;

    /// <summary>The WAVE number shown on the HUD (continues counting on later cycles).</summary>
    public int Wave { get; set; } = 1;

    public bool CanRefuel => Fuel < FuelYellow;

    public bool Chance(double perTick) => Rng.NextDouble() < perTick;

    public void Burn()
    {
        if (Tick % FuelBurnTicks == 0 && Fuel > 0)
            Fuel--;
    }

    public void AddBlast(int centreX, int centreY)
    {
        Blasts.Add(new Blast { X = centreX - 5, Y = centreY - 6, Timer = 12 });
    }

    public void DropBomb(float x, float y, Sprite sprite, float speed)
    {
        int max = 2 + Difficulty / 3;
        if (Bombs.Count >= max)
            return;
        Bombs.Add(new Bomb { X = x - 2, Y = y, Speed = speed, Sprite = sprite });
        Sound.Play(Sfx.Bomb);
    }

    /// <summary>Moves bombs; returns true if one hit the given sprite.</summary>
    public bool UpdateBombs(Sprite target, int tx, int ty, int floor = PlayBottom)
    {
        bool hit = false;
        for (int i = Bombs.Count - 1; i >= 0; i--)
        {
            var b = Bombs[i];
            b.Y += b.Speed;
            if (target != null && Sprite.Overlaps(b.Sprite, (int)b.X, (int)b.Y, target, tx, ty))
            {
                hit = true;
                Bombs.RemoveAt(i);
            }
            else if (b.Y + b.Sprite.Height >= floor)
            {
                Bombs.RemoveAt(i);
            }
        }
        return hit;
    }

    public void UpdateBlasts()
    {
        for (int i = Blasts.Count - 1; i >= 0; i--)
            if (--Blasts[i].Timer <= 0)
                Blasts.RemoveAt(i);
    }

    public void ClearProjectiles()
    {
        Bombs.Clear();
        Laser.Active = false;
    }

    public void DrawCommon(Canvas c)
    {
        foreach (var b in Bombs)
            c.Draw(b.Sprite, (int)b.X, (int)b.Y);
        Laser.Draw(c);
        var star = Sprite.Of(SpriteData.Explosion);
        foreach (var b in Blasts)
            c.Draw(star, b.X, b.Y);
    }

    /// <summary>The fuel gauge across the top: red, yellow then green, with FUEL in white.</summary>
    public void DrawFuel(Canvas c)
    {
        DrawFuelGauge(c, Fuel, Tick);
    }

    public static void DrawFuelGauge(Canvas c, int fuel, int tick)
    {
        int f = Math.Clamp(fuel, 0, MaxFuel);
        bool flash = f < FuelRed && tick / 12 % 2 == 0;
        if (!flash)
        {
            c.Fill(0, 0, Math.Min(f, FuelRed), 6, OricColor.Red);
            if (f > FuelRed)
                c.Fill(FuelRed, 0, Math.Min(f, FuelYellow) - FuelRed, 6, OricColor.Yellow);
            if (f > FuelYellow)
                c.Fill(FuelYellow, 0, f - FuelYellow, 6, OricColor.Green);
        }
        c.TextAt(36, 0, "FUEL", OricColor.White);
    }

    /// <summary>The blue status lines with yellow text, exactly as laid out on the Oric.</summary>
    public void DrawHud(Canvas c) => DrawHud(c, Session, Wave);

    public static void DrawHud(Canvas c, GameSession s, int wave)
    {
        c.Fill(0, HudTop, Canvas.Width, Canvas.Height - HudTop, OricColor.Blue);
        c.TextAt(2, 200, "SCORE", OricColor.Yellow);
        c.TextAt(17, 200, "BASES", OricColor.Yellow);
        c.TextAt(30, 200, "HI-SCORE", OricColor.Yellow);
        c.TextAt(2, 208, s.Score.ToString("D6"), OricColor.Yellow);
        c.TextAt(19, 208, s.Bases.ToString(), OricColor.Yellow);
        c.TextAt(31, 208, s.HiScore.ToString("D6"), OricColor.Yellow);
        c.TextAt(9, 216, "SKILL " + s.Skill, OricColor.Yellow);
        c.TextAt(23, 216, "WAVE " + Math.Min(wave, 99).ToString("D2"), OricColor.Yellow);
    }
}

/// <summary>The player's laser: a 2x7 white bolt.</summary>
public sealed class Laser
{
    public const int Width = 2;
    public const int Height = 7;
    public const float Speed = 6f;

    public bool Active;
    public float X, Y;

    public bool TryFire(float x, float y)
    {
        if (Active)
            return false;
        Active = true;
        X = x;
        Y = y;
        return true;
    }

    public void Update()
    {
        if (!Active)
            return;
        Y -= Speed;
        if (Y + Height < XenonWorld.PlayTop)
            Active = false;
    }

    public bool Hits(Sprite s, float sx, float sy) =>
        Active && Sprite.OverlapsRect(s, (int)sx, (int)sy, (int)X, (int)Y, Width, Height);

    public void Draw(Canvas c)
    {
        if (Active)
            c.Fill((int)X, (int)Y, Width, Height, OricColor.White);
    }
}

/// <summary>The yellow Xenon fighter at the foot of the screen.</summary>
public sealed class PlayerShip
{
    public const int Top = 180;
    public const float Speed = 2.5f;
    public static Sprite Sprite => Sprite.Of(SpriteData.PlayerShip, nameof(SpriteData.PlayerShip));

    public float X = (Canvas.Width - 11) / 2f;
    public int Y = Top;

    public int CentreX => (int)X + Sprite.Width / 2;

    public void Update(XenonWorld w, bool canFire)
    {
        X = Math.Clamp(w.Controls.Steer(X, Speed, false, Sprite.Width / 2f), 0, Canvas.Width - Sprite.Width);
        if (canFire && (w.Controls.FirePressed || w.Controls.FireHeld) &&
            w.Laser.TryFire(X + Sprite.Width / 2f - 1, Y - Laser.Height))
            w.Sound.Play(Sfx.Shoot);
    }

    public void Draw(Canvas c) => c.Draw(Sprite, (int)X, Y);
}
