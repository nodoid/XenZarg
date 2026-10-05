using System.Collections.Generic;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon;

public enum MissionKind { Spider, Reptile, Ship, Bird }

/// <summary>One playable Zorgon's Revenge screen.</summary>
public abstract class ZorgonMission
{
    protected ZorgonMission(ZorgonWorld world)
    {
        W = world;
    }

    protected ZorgonWorld W { get; }

    public abstract OricColor HudPaper { get; }
    public abstract OricColor HudInk { get; }

    /// <summary>The bonus shown when the screen starts.</summary>
    public abstract int StartBonus { get; }

    public virtual bool BonusCountsDown => true;

    /// <summary>The castle shows a bonus that the original never paid out.</summary>
    public virtual bool AwardsBonus => true;

    public bool Completed { get; protected set; }
    public bool Failed { get; protected set; }

    /// <summary>When set, enemies and hazards cannot kill the hero (used by tests and demos).
    /// Falls, the river and riding a lift through the end of its shaft still kill.</summary>
    public bool Invulnerable { get; set; }

    public abstract void Update();

    public abstract void Draw(Canvas c);
}

/// <summary>A screen where the hero walks and jumps between surfaces.</summary>
public abstract class PlatformMission : ZorgonMission
{
    protected PlatformMission(ZorgonWorld world, float heroX, float heroFeet) : base(world)
    {
        Hero = new Hero(heroX, heroFeet);
    }

    public Hero Hero { get; }

    protected readonly List<Surface> Surfaces = new();

    /// <summary>Runs hero physics; returns true if he landed this tick.</summary>
    protected bool UpdateHero(IReadOnlyList<Surface> surfaces = null)
    {
        bool landed = Hero.Update(W, surfaces ?? Surfaces);
        if (Hero.IsDead)
            Failed = true;
        return landed;
    }

    protected void Kill()
    {
        if (!Invulnerable)
            Hero.Die(W.Sound);
    }
}

/// <summary>The magenta lift bar running in the shaft on the right of the platform screens.</summary>
public sealed class Lift
{
    public const int X = 216;
    public const int Width = 18;
    public const int Highest = 19;
    public const int Lowest = 194;

    /// <summary>Hero's head centre while riding (on the left half, ready to step off).</summary>
    public const float RideX = 221;

    public Lift(float y, float speed, int direction)
    {
        Y = y;
        Speed = speed;
        Direction = direction;
    }

    /// <summary>Row of the top of the magenta bar.</summary>
    public float Y { get; set; }
    public float Speed { get; }

    /// <summary>-1 rising, +1 descending.</summary>
    public int Direction { get; set; }

    /// <summary>True on the tick the lift wrapped round the shaft.</summary>
    public bool Wrapped { get; private set; }

    public Surface Surface => new(X - 6, X + Width - 1, (int)Y);

    public void Update()
    {
        Wrapped = false;
        Y += Direction * Speed;
        if (Direction < 0 && Y < Highest)
        {
            Y = Lowest;
            Wrapped = true;
        }
        else if (Direction > 0 && Y > Lowest)
        {
            Y = Highest;
            Wrapped = true;
        }
    }

    /// <summary>Boards the hero if he just landed on the bar; keeps him on it; kills him at a wrap.</summary>
    public void Carry(Hero hero, bool landed)
    {
        if (landed && hero.Current == Hero.State.Standing && System.Math.Abs(hero.Feet - Y) < 1f && hero.X >= X - 6)
            hero.Ride(this, RideX, Y);
        if (hero.Carrier == this)
        {
            hero.Carry(RideX, Y);
        }
    }

    public void Draw(Canvas c) => c.Draw(SpriteData.ZLiftPlatform, X, (int)Y - 1);
}
