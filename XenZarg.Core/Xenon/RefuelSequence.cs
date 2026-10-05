using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Xenon;

/// <summary>
/// After pressing DISENGAGE the fighter climbs through the lightning barriers to dock
/// with the mothership's flashing service bay. Fuel keeps burning on the way up.
/// Holding FIRE (or keeping a finger on the screen) applies the brake and hovers.
/// </summary>
public sealed class RefuelSequence
{
    public const int MotherX = 84, MotherY = 10;
    public const int BayWidth = 14, BayHeight = 4;
    public const int GapWidth = 30;
    public const float ClimbSpeed = 0.8f;
    public const int DockTolerance = 6;
    public static readonly int[] BarrierRows = [64, 104, 144];

    public enum State { Climbing, Docked, Done }

    private static Sprite _mothership;
    private readonly XenonWorld _w;
    private float _x;
    private float _y = PlayerShip.Top;
    private int _dockTimer;
    private readonly float[] _phase;

    public RefuelSequence(XenonWorld world, float shipX)
    {
        _w = world;
        _x = shipX;
        _phase = new float[BarrierRows.Length];
        for (int i = 0; i < _phase.Length; i++)
            _phase[i] = (float)(world.Rng.NextDouble() * Math.PI * 2);
        world.ClearProjectiles();
        world.Sound.Play(Sfx.Refuel);
    }

    public static Sprite Mothership => _mothership ??= Sprite.Of(SpriteData.Saucer2).Scale(3, 2);

    public State Current { get; private set; } = State.Climbing;
    public bool Crashed { get; private set; }
    public float ShipX => _x;
    public float ShipY => _y;

    public static int BayX => MotherX + Mothership.Width / 2 - BayWidth / 2;
    public static int BayY => MotherY + Mothership.Height;

    /// <summary>Left edge of the safe gap in a barrier at a given tick.</summary>
    public int GapX(int barrier) =>
        (int)(Canvas.Width / 2 - GapWidth / 2 + Math.Sin(_w.Tick * (0.012 + 0.003 * barrier) + _phase[barrier]) * 75);

    public void Update()
    {
        switch (Current)
        {
            case State.Climbing:
                Climb();
                break;
            case State.Docked:
                _w.Fuel = Math.Min(XenonWorld.MaxFuel, _w.Fuel + 2);
                if (_w.Tick % 15 == 0)
                    _w.Sound.Play(Sfx.Refuel, 0.3f);
                if (_w.Fuel >= XenonWorld.MaxFuel && ++_dockTimer > 25)
                    Current = State.Done;
                break;
        }
    }

    private void Climb()
    {
        _w.Burn();
        var ship = PlayerShip.Sprite;
        _x = Math.Clamp(_w.Controls.Steer(_x, PlayerShip.Speed, false, ship.Width / 2f), 0, Canvas.Width - ship.Width);
        if (!_w.Controls.FireHeld)
            _y -= ClimbSpeed;

        for (int b = 0; b < BarrierRows.Length; b++)
        {
            int row = BarrierRows[b];
            if (_y > row + 2 || _y + ship.Height < row)
                continue;
            int gap = GapX(b);
            bool inGap = _x >= gap && _x + ship.Width <= gap + GapWidth;
            if (!inGap)
            {
                Crashed = true;
                Current = State.Done;
                return;
            }
        }

        if (_y <= BayY + 1)
        {
            float centre = _x + ship.Width / 2f;
            if (Math.Abs(centre - (BayX + BayWidth / 2f)) <= DockTolerance)
            {
                _y = BayY + 1;
                Current = State.Docked;
                _w.Sound.Play(Sfx.Docked);
            }
            else
            {
                // Missed the bay: bounce off the hull and try again.
                _y += 40;
                _w.Sound.Play(Sfx.Warning);
            }
        }
    }

    public void Draw(Canvas c)
    {
        c.Draw(Mothership, MotherX, MotherY);
        var bayColor = (OricColor)(1 + _w.Tick / 4 % 7);
        c.Fill(BayX, BayY, BayWidth, BayHeight, bayColor);

        for (int b = 0; b < BarrierRows.Length; b++)
        {
            int row = BarrierRows[b];
            int gap = GapX(b);
            for (int x = 0; x < Canvas.Width; x++)
            {
                if (x >= gap && x < gap + GapWidth)
                    continue;
                // Jagged lightning: a zig-zag that crawls along the barrier.
                int zig = ((x + _w.Tick) / 3) % 4;
                int dy = zig switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 };
                c.Plot(x, row + dy, (x / 3 + _w.Tick) % 7 == 0 ? OricColor.White : OricColor.Cyan);
            }
        }

        if (!Crashed)
            c.Draw(PlayerShip.Sprite, (int)_x, (int)_y);
    }
}
