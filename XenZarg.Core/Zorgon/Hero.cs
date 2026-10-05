using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;

namespace XenZarg.Core.Zorgon;

/// <summary>
/// "Our Hero": walks, jumps a fixed 20-row arc, rides lifts, ropes and birds.
/// Walking off an edge, landing too far below the jump peak or landing in the river is fatal,
/// exactly as in the original. <see cref="X"/> is the centre of his head, <see cref="Feet"/> the
/// row he stands on.
/// </summary>
public sealed class Hero
{
    public enum State { Standing, Jumping, Falling, Riding, Dying }

    public const int Height = 23;
    public const int JumpRows = 20;
    public const int FatalDrop = 32;
    public const int DyingTicks = 75;

    // Original ratios (spider screen, 250 loops/s): walk 1 px per 25 loops, jump 1 row per 5 loops,
    // jump drift 2 px per 25 loops.
    public static readonly float WalkSpeed = ZorgonWorld.PerTick(10f);
    public static readonly float JumpStep = ZorgonWorld.PerTick(50f);
    public static readonly float JumpDrift = ZorgonWorld.PerTick(20f);

    private static readonly Sprite[] WalkRight = Frames(SpriteData.ZHeroWalkRightF0, SpriteData.ZHeroWalkRightF1, SpriteData.ZHeroWalkRightF2, SpriteData.ZHeroWalkRightF3, SpriteData.ZHeroWalkRightF4, SpriteData.ZHeroWalkRightF5);
    private static readonly Sprite[] WalkLeft = Frames(SpriteData.ZHeroWalkLeftF0, SpriteData.ZHeroWalkLeftF1, SpriteData.ZHeroWalkLeftF2, SpriteData.ZHeroWalkLeftF3, SpriteData.ZHeroWalkLeftF4, SpriteData.ZHeroWalkLeftF5);
    private static readonly Sprite[] JumpRight = Frames(SpriteData.ZHeroJumpRightF0, SpriteData.ZHeroJumpRightF1, SpriteData.ZHeroJumpRightF2, SpriteData.ZHeroJumpRightF3, SpriteData.ZHeroJumpRightF4, SpriteData.ZHeroJumpRightF5);
    private static readonly Sprite[] JumpLeft = Frames(SpriteData.ZHeroJumpLeftF0, SpriteData.ZHeroJumpLeftF1, SpriteData.ZHeroJumpLeftF2, SpriteData.ZHeroJumpLeftF3, SpriteData.ZHeroJumpLeftF4, SpriteData.ZHeroJumpLeftF5);

    private static Sprite[] Frames(params string[][] maps)
    {
        var s = new Sprite[maps.Length];
        for (int i = 0; i < maps.Length; i++)
            s[i] = Sprite.Of(maps[i]);
        return s;
    }

    private float _peak;
    private bool _rising;
    private int _jumpDir;
    private float _riseLeft;

    public Hero(float x, float feet)
    {
        X = x;
        Feet = feet;
    }

    public float X { get; set; }
    public float Feet { get; set; }
    public float Top => Feet - Height;
    public bool FacingLeft { get; set; }
    public State Current { get; private set; } = State.Standing;
    public int DyingTimer { get; private set; }
    public bool IsDead => Current == State.Dying && DyingTimer <= 0;
    public bool Airborne => Current is State.Jumping or State.Falling;
    public bool Rising => Current == State.Jumping && _rising;

    /// <summary>The object carrying the hero (lift, rope, bird), if riding.</summary>
    public object Carrier { get; private set; }

    /// <summary>Left / right limits the hero may walk to.</summary>
    public float MinX { get; set; } = 6;
    public float MaxX { get; set; } = 233;

    public void Die(ISoundPlayer sound)
    {
        if (Current == State.Dying)
            return;
        Current = State.Dying;
        DyingTimer = DyingTicks;
        Carrier = null;
        sound.Play(Sfx.PlayerDeath);
    }

    public void Ride(object carrier, float x, float feet)
    {
        Current = State.Riding;
        Carrier = carrier;
        X = x;
        Feet = feet;
    }

    /// <summary>Moves with the carrier.</summary>
    public void Carry(float x, float feet)
    {
        X = x;
        Feet = feet;
    }

    /// <summary>Drops from a carrier as if from the top of a jump (used when letting go of the bird).</summary>
    public void Release()
    {
        Carrier = null;
        Current = State.Jumping;
        _rising = false;
        _peak = Feet;
        _jumpDir = 0;
    }

    /// <summary>Steering wish: -1, 0 or 1 (mouse targets are converted by the caller).</summary>
    public static int Wish(ZorgonWorld w, float heroX)
    {
        var c = w.Controls;
        if (c.Pointer is Microsoft.Xna.Framework.Vector2 p)
        {
            float d = p.X - heroX;
            return Math.Abs(d) < 1.5f ? 0 : Math.Sign(d);
        }
        if (c.MoveX < -0.3f) return -1;
        if (c.MoveX > 0.3f) return 1;
        return 0;
    }

    /// <summary>
    /// One tick of hero control and physics.
    /// <paramref name="surfaces"/> are the solid floors; returns true if the hero landed this tick.
    /// </summary>
    public bool Update(ZorgonWorld w, IReadOnlyList<Surface> surfaces)
    {
        switch (Current)
        {
            case State.Dying:
                DyingTimer--;
                Feet = Math.Min(Feet + 0.5f, ZorgonWorld.Bottom + Height);
                return false;
            case State.Riding:
                if (w.Controls.FirePressed)
                    StartJump(w, Wish(w, X));
                return false;
            case State.Standing:
                return UpdateStanding(w, surfaces);
            default:
                return UpdateAirborne(w, surfaces);
        }
    }

    private bool UpdateStanding(ZorgonWorld w, IReadOnlyList<Surface> surfaces)
    {
        int wish = Wish(w, X);
        if (w.Controls.FirePressed)
        {
            StartJump(w, wish);
            return false;
        }
        if (wish != 0)
        {
            FacingLeft = wish < 0;
            float before = X;
            X = Math.Clamp(X + wish * WalkSpeed, MinX, MaxX);
            if ((int)before / 3 != (int)X / 3 && (int)X % 6 == 0)
                w.Sound.Play(Sfx.Step);
        }
        if (FindSurface(surfaces, X, Feet) is not Surface)
        {
            // Walked off an edge: always fatal on the Oric.
            Current = State.Falling;
            _peak = Feet;
            _jumpDir = 0;
            w.Sound.Play(Sfx.Fall);
        }
        return false;
    }

    public void StartJump(ZorgonWorld w, int dir)
    {
        bool fromCarrier = Current == State.Riding;
        Carrier = null;
        Current = State.Jumping;
        _rising = true;
        _riseLeft = JumpRows;
        _jumpDir = dir;
        if (dir != 0)
            FacingLeft = dir < 0;
        // Leaping off a lift or rope is judged from the carrier's height.
        _peak = fromCarrier ? Feet : Feet - JumpRows;
        w.Sound.Play(Sfx.Jump);
    }

    private bool UpdateAirborne(ZorgonWorld w, IReadOnlyList<Surface> surfaces)
    {
        X = Math.Clamp(X + _jumpDir * JumpDrift, MinX, MaxX);
        if (Current == State.Jumping && _rising)
        {
            float step = Math.Min(JumpStep, _riseLeft);
            Feet -= step;
            _riseLeft -= step;
            if (_riseLeft <= 0)
                _rising = false;
            return false;
        }

        float before = Feet;
        Feet += JumpStep;
        foreach (var s in surfaces)
        {
            // One row of slack so a lift rising into the hero is still caught.
            if (!s.Contains(X) || before > s.Top + 1f || Feet < s.Top)
                continue;
            Feet = s.Top;
            if (Current == State.Falling || Feet - _peak >= FatalDrop || s.Deadly)
            {
                Die(w.Sound);
                return false;
            }
            Current = State.Standing;
            w.Sound.Play(Sfx.Land);
            return true;
        }
        if (Feet - _peak >= FatalDrop + 40 || Top > ZorgonWorld.Bottom)
            Die(w.Sound);
        return false;
    }

    public static Surface? FindSurface(IReadOnlyList<Surface> surfaces, float x, float feet)
    {
        foreach (var s in surfaces)
            if (s.Contains(x) && Math.Abs(s.Top - feet) < 0.6f)
                return s;
        return null;
    }

    /// <summary>The current frame and where to draw it so the head centre lands on <see cref="X"/>.</summary>
    public (Sprite sprite, int x, int y) Frame()
    {
        int xi = (int)Math.Floor(X);
        if (Current == State.Dying)
            return (Sprite.Of(SpriteData.ZHeroFallingDying), xi - 9, (int)Top);
        bool jumping = Airborne;
        int f;
        float offset;
        if (FacingLeft)
        {
            f = (6 - ((xi % 6) + 6) % 6) % 6;
            offset = (jumping ? 16.5f : 17.5f) - f;
            return ((jumping ? JumpLeft : WalkLeft)[f], (int)(xi - offset + 0.5f), (int)Top);
        }
        f = ((xi % 6) + 6) % 6;
        offset = (jumping ? 10.5f : 11.5f) + f;
        return ((jumping ? JumpRight : WalkRight)[f], (int)(xi - offset + 0.5f), (int)Top);
    }

    public void Draw(Canvas c)
    {
        if (Current == State.Dying && DyingTimer / 4 % 2 == 0)
            return;
        var (s, x, y) = Frame();
        c.Draw(s, x, y);
    }

    /// <summary>Pixel-perfect test of the hero against another sprite.</summary>
    public bool Touches(Sprite other, int ox, int oy)
    {
        if (Current == State.Dying)
            return false;
        var (s, x, y) = Frame();
        return Sprite.Overlaps(s, x, y, other, ox, oy);
    }

    public bool TouchesRect(int rx, int ry, int rw, int rh)
    {
        if (Current == State.Dying)
            return false;
        var (s, x, y) = Frame();
        return Sprite.OverlapsRect(s, x, y, rx, ry, rw, rh);
    }
}
