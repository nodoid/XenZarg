using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Genesis;

public sealed class Meteor
{
    public float X;
    public float Y;
    public float Speed;
}

/// <summary>
/// The hyperspace run: raise the speed into the red markers and hold it there until
/// the jump gauge fills. The probe flies left to right across the whole screen, and
/// meteors stream in from the right and knock the speed down.
/// </summary>
public sealed class Hyperdrive
{
    public const float MinSpeed = 0.42f;
    public const float MaxSpeed = 0.70f;
    public const float Acceleration = 0.018f;
    public const float Braking = 0.024f;

    /// <summary>Ticks to fill the jump gauge while the speed is in the band.</summary>
    public const int ChargeTicks = 350;

    /// <summary>The game's tick rate, which the time-to-jump counter counts in.</summary>
    public const int TicksPerSecond = XenZargGame.TicksPerSecond;

    /// <summary>The two horizontal gauges along the top of the screen.</summary>
    public const int BarX = 8;
    public const int BarWidth = 192;
    public const int BarHeight = 5;
    public const int SpeedBarY = 4;
    public const int ChargeBarY = 13;

    /// <summary>Top of the star field, below the gauges and the jump counter.</summary>
    public const int FieldTop = 32;

    /// <summary>Where the probe sits, flying towards the right-hand edge.</summary>
    public const int ShipX = 26;
    public const int ShipY = 132;

    private const int HintTop = 178;

    private readonly Random _rng;
    private readonly ISoundPlayer _sound;
    private int _meteorTimer;
    private int _stallTicks;
    private int _tick;

    public Hyperdrive(Random rng, ISoundPlayer sound)
    {
        _rng = rng ?? new Random();
        _sound = sound ?? SilentSoundPlayer.Instance;
    }

    public float Speed { get; private set; } = 0.12f;
    public float Charge { get; private set; }
    public bool Completed { get; private set; }
    public bool Failed { get; private set; }
    public List<Meteor> Meteors { get; } = new();

    public bool InBand => Speed >= MinSpeed && Speed <= MaxSpeed;

    /// <summary>Seconds of in-band flight still needed before the jump completes.</summary>
    public int SecondsLeft =>
        (int)Math.Ceiling((1 - Math.Clamp(Charge, 0f, 1f)) * ChargeTicks / TicksPerSecond);

    public void Reset()
    {
        Speed = 0.12f;
        Charge = 0;
        Completed = false;
        Failed = false;
        _stallTicks = 0;
        _meteorTimer = 0;
        Meteors.Clear();
    }

    public void Update(Input.IControls controls)
    {
        if (Completed || Failed)
            return;
        _tick++;

        float wish = controls.MoveY;
        if (controls.FirePressed)
            wish = -1;
        // Over the speed bar the mouse sets the throttle directly, like a lever.
        if (controls.Pointer is Microsoft.Xna.Framework.Vector2 p &&
            p.X >= BarX && p.X <= BarX + BarWidth &&
            p.Y >= SpeedBarY - 2 && p.Y <= ChargeBarY + BarHeight + 2)
        {
            float target = Math.Clamp((p.X - BarX) / (float)BarWidth, 0, 1);
            wish = target > Speed + 0.005f ? -1 : target < Speed - 0.005f ? 1 : 0;
        }
        if (wish < -0.3f)
            Speed += Acceleration;
        else if (wish > 0.3f)
            Speed -= Braking;
        else
            Speed += 0.0035f;

        Speed = Math.Clamp(Speed, 0, 1);

        if (InBand)
            Charge = Math.Min(1, Charge + 1f / ChargeTicks);
        else
            Charge = Math.Max(0, Charge - 1f / (ChargeTicks * 2));

        if (Speed < 0.04f)
            _stallTicks++;
        else
            _stallTicks = 0;
        if (_stallTicks > 150)
        {
            Failed = true;
            return;
        }

        if (--_meteorTimer <= 0)
        {
            _meteorTimer = Math.Max(8, 20 - (int)(Speed * 8));
            Meteors.Add(new Meteor
            {
                X = Canvas.Width + 8,
                Y = FieldTop + (float)_rng.NextDouble() * (HintTop - FieldTop - 10),
                Speed = 1.4f + (float)_rng.NextDouble() * 1.2f + Speed * 1.6f,
            });
        }

        for (int i = Meteors.Count - 1; i >= 0; i--)
        {
            var m = Meteors[i];
            m.X -= m.Speed;
            if (Sprite.Overlaps(Sprite.Of(SpriteData.Meteor1), (int)m.X, (int)m.Y,
                    Sprite.Of(SpriteData.GShip), ShipX, ShipY))
            {
                Meteors.RemoveAt(i);
                Speed = Math.Max(0, Speed - 0.14f);
                _sound.Play(Sfx.Bomb);
                continue;
            }
            if (m.X < -Sprite.Of(SpriteData.Meteor1).Width)
                Meteors.RemoveAt(i);
        }

        if (Charge >= 1)
        {
            Completed = true;
            _sound.Play(Sfx.Docked);
        }
    }

    public void Draw(Canvas c)
    {
        c.Fill(0, 0, Canvas.Width, Canvas.Height, OricColor.Black);

        DrawStars(c);

        foreach (var m in Meteors)
            c.Draw(SpriteData.Meteor1, (int)m.X, (int)m.Y);

        c.Draw(SpriteData.GShip, ShipX, ShipY);

        DrawGauges(c);
        DrawHintLine(c);
    }

    /// <summary>The star streaks, drifting right to left as the probe races right.</summary>
    private void DrawStars(Canvas c)
    {
        int bottom = Math.Min(Canvas.Height, HintTop);
        for (int i = 0; i < 90; i++)
        {
            int y = FieldTop + (i * 41 + 7) % (bottom - FieldTop);
            float drift = _tick * (0.6f + Speed * 2.6f) * (1 + i % 3);
            int x = Wrap(Canvas.Width - i * 29 - drift, Canvas.Width);
            int length = 1 + (int)(Speed * 4) + i % 2;
            c.Fill(x, y, length, 1, i % 5 == 0 ? OricColor.White : OricColor.Cyan);
        }
    }

    private void DrawGauges(Canvas c)
    {
        c.Fill(0, 0, Canvas.Width, FieldTop - 2, OricColor.Black);

        c.Text(1, SpeedBarY - 1, "S", OricColor.Cyan);
        c.Fill(BarX, SpeedBarY, BarWidth, BarHeight, OricColor.Blue);
        int speed = (int)(Math.Clamp(Speed, 0, 1) * BarWidth);
        if (speed > 0)
            c.Fill(BarX, SpeedBarY, speed, BarHeight, OricColor.Yellow);
        for (int x = BarX; x < BarX + BarWidth; x += 8)
            c.Fill(x, SpeedBarY, 1, BarHeight, OricColor.Black);
        // The band the jump needs, as a pair of red markers.
        int low = BarX + (int)(MinSpeed * BarWidth);
        int high = BarX + (int)(MaxSpeed * BarWidth);
        c.Fill(low, SpeedBarY - 1, 1, BarHeight + 2, OricColor.Red);
        c.Fill(high, SpeedBarY - 1, 1, BarHeight + 2, OricColor.Red);

        c.Text(1, ChargeBarY - 1, "H", OricColor.Cyan);
        c.Fill(BarX, ChargeBarY, BarWidth, BarHeight, OricColor.Blue);
        int charge = (int)(Math.Clamp(Charge, 0, 1) * BarWidth);
        if (charge > 0)
            c.Fill(BarX, ChargeBarY, charge, BarHeight, OricColor.White);
        for (int x = BarX; x < BarX + BarWidth; x += 8)
            c.Fill(x, ChargeBarY, 1, BarHeight, OricColor.Black);

        int seconds = SecondsLeft;
        c.Text(1, ChargeBarY + BarHeight + 3,
            "TIME TO JUMP " + (seconds / 60).ToString("D2") + ":" + (seconds % 60).ToString("D2"),
            OricColor.Yellow);
    }

    private static void DrawHintLine(Canvas c)
    {
        c.Fill(0, HintTop, Canvas.Width, Canvas.Height - HintTop, OricColor.Black);
        c.TextCentered(HintTop + 1, "SPACE / UP: ACCELERATE   DOWN: BRAKE", OricColor.Cyan);
        c.TextCentered(HintTop + 9, "MOUSE ON THE SPEED BAR SETS THE THROTTLE", OricColor.White);
    }

    private static int Wrap(float value, int span)
    {
        int v = (int)value % span;
        return v < 0 ? v + span : v;
    }
}
