using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace XenZarg.Core.Input;

/// <summary>The per-tick controls a game screen reads. Implemented by <see cref="InputManager"/>.</summary>
public interface IControls
{
    float MoveX { get; }
    float MoveY { get; }
    Vector2? Pointer { get; }

    /// <summary>New touches (or mouse clicks) this tick, in virtual pixels.</summary>
    IReadOnlyList<Vector2> Taps { get; }

    bool FirePressed { get; }
    bool FireHeld { get; }
    bool DisengagePressed { get; }
    bool LeftPressed { get; }
    bool RightPressed { get; }
    bool UpPressed { get; }
    bool DownPressed { get; }

    /// <summary>See <see cref="InputManager.Steer"/>.</summary>
    float Steer(float position, float speed, bool vertical, float pointerOffset = 0);
}
