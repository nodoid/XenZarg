using System;
using System.Linq;
using CoreMotion;
using Microsoft.Xna.Framework;
using UIKit;
using XenZarg.Core.Platform;

namespace XenZarg.iOS;

/// <summary>
/// Tilt steering from CoreMotion device motion, which fuses the gyroscope and
/// accelerometer into a smooth gravity vector.
/// </summary>
public sealed class IosTiltSensor : ITiltSensor
{
    private readonly CMMotionManager _motion = new();

    public bool IsAvailable => _motion.DeviceMotionAvailable;

    public void Start()
    {
        if (_motion.DeviceMotionAvailable && !_motion.DeviceMotionActive)
        {
            _motion.DeviceMotionUpdateInterval = 1.0 / 60.0;
            _motion.StartDeviceMotionUpdates();
        }
    }

    public void Stop() => _motion.StopDeviceMotionUpdates();

    public Vector2 Read()
    {
        var gravity = _motion.DeviceMotion?.Gravity;
        if (gravity == null)
            return Vector2.Zero;

        // Gravity is in portrait device axes (y towards the top of the device) in g.
        // In LandscapeRight the top of the device points left: screen-right is -y, screen-up is +x.
        bool left = CurrentOrientation() == UIInterfaceOrientation.LandscapeLeft;
        float gx = (float)gravity.Value.X, gy = (float)gravity.Value.Y;
        float screenRight = left ? gy : -gy;
        float screenUp = left ? -gx : gx;
        // X: right-hand edge dipping is positive. Y: top edge tipping away is positive.
        return new Vector2(Math.Clamp(screenRight, -1f, 1f), Math.Clamp(screenUp, -1f, 1f));
    }

    private static UIInterfaceOrientation CurrentOrientation()
    {
        var scene = UIApplication.SharedApplication.ConnectedScenes.ToArray().OfType<UIWindowScene>().FirstOrDefault();
        if (scene == null)
            return UIInterfaceOrientation.LandscapeRight;
        if (OperatingSystem.IsIOSVersionAtLeast(26))
            return scene.EffectiveGeometry.InterfaceOrientation;
#pragma warning disable CA1422
        return scene.InterfaceOrientation;
#pragma warning restore CA1422
    }
}
