using System;
using Microsoft.Xna.Framework;
using XenZarg.Core.Audio;
using XenZarg.Core.Input;
using XenZarg.Core.Session;
using XenZarg.Core.Xenon;

namespace XenZarg.Tests;

/// <summary>Scriptable controls for driving game logic without a window.</summary>
public sealed class FakeControls : IControls
{
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public Vector2? Pointer { get; set; }
    public bool FirePressed { get; set; }
    public bool FireHeld { get; set; }
    public bool DisengagePressed { get; set; }
    public bool LeftPressed { get; set; }
    public bool RightPressed { get; set; }
    public bool UpPressed { get; set; }
    public bool DownPressed { get; set; }

    public float Steer(float position, float speed, bool vertical, float pointerOffset = 0)
    {
        if (Pointer is Vector2 p)
        {
            float target = (vertical ? p.Y : p.X) - pointerOffset;
            float delta = target - position;
            return Math.Abs(delta) <= speed ? target : position + Math.Sign(delta) * speed;
        }
        return position + (vertical ? MoveY : MoveX) * speed;
    }
}

public sealed class RecordingSound : ISoundPlayer
{
    public System.Collections.Generic.List<Sfx> Played { get; } = new();
    public void Play(Sfx sfx, float pitch = 0f) => Played.Add(sfx);
}

public static class TestWorld
{
    public static XenonWorld Create(out FakeControls controls, int skill = 5, int seed = 42, RecordingSound sound = null)
    {
        controls = new FakeControls();
        var session = new GameSession(skill, 0);
        return new XenonWorld(session, controls, sound ?? new RecordingSound(), new Random(seed));
    }

    /// <summary>Advances a level one tick the way <see cref="XenonScene"/> does.</summary>
    public static void Step(XenonWorld world, XenonLevel level)
    {
        world.Tick++;
        world.UpdateBlasts();
        level.Update();
    }
}
