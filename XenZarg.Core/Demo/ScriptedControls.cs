using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using XenZarg.Core.Input;

namespace XenZarg.Core.Demo;

/// <summary>Controls set by code: used by the attract demos, store captures and tests.</summary>
public sealed class ScriptedControls : IControls
{
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public Vector2? Pointer { get; set; }
    public List<Vector2> TapList { get; } = new();
    public IReadOnlyList<Vector2> Taps => TapList;
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

    /// <summary>Clears one-tick presses after a tick has used them.</summary>
    public void EndTick()
    {
        TapList.Clear();
        FirePressed = false;
        DisengagePressed = false;
        LeftPressed = RightPressed = UpPressed = DownPressed = false;
    }
}
