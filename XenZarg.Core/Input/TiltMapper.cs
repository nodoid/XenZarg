using System;
using Microsoft.Xna.Framework;

namespace XenZarg.Core.Input;

/// <summary>
/// Converts raw gravity readings into -1..1 steering values with a dead zone,
/// a calibrated neutral pitch and light smoothing.
/// </summary>
public sealed class TiltMapper
{
    /// <summary>Tilt (in g) that gives full deflection, roughly 20 degrees.</summary>
    public float FullScale { get; set; } = 0.34f;

    /// <summary>Tilt (in g) ignored around neutral, roughly 3.5 degrees.</summary>
    public float DeadZone { get; set; } = 0.06f;

    /// <summary>0 = no smoothing, approaching 1 = heavy smoothing.</summary>
    public float Smoothing { get; set; } = 0.35f;

    public float NeutralY { get; private set; }

    private Vector2 _smoothed;
    private Vector2 _lastRaw;

    public void Calibrate() => NeutralY = _lastRaw.Y;

    public Vector2 Map(Vector2 raw)
    {
        _lastRaw = raw;
        _smoothed = Vector2.Lerp(raw, _smoothed, Smoothing);
        return new Vector2(Axis(_smoothed.X), Axis(_smoothed.Y - NeutralY));
    }

    public float Axis(float g)
    {
        float mag = Math.Abs(g);
        if (mag <= DeadZone)
            return 0f;
        float v = (mag - DeadZone) / (FullScale - DeadZone);
        return Math.Sign(g) * Math.Min(1f, v);
    }
}
