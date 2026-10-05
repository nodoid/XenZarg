using Android.Content;
using Android.Hardware;
using Android.Runtime;
using Android.Views;
using Microsoft.Xna.Framework;
using XenZarg.Core.Platform;

namespace XenZarg.Android;

/// <summary>
/// Tilt steering from the gyroscope-fused gravity sensor, falling back to a low-pass
/// filtered accelerometer on devices without a gyroscope.
/// </summary>
public sealed class AndroidTiltSensor : Java.Lang.Object, ITiltSensor, ISensorEventListener
{
    private const float Gravity = SensorManager.GravityEarth;

    private readonly SensorManager _manager;
    private readonly Sensor _sensor;
    private readonly bool _filter;
    private readonly IWindowManager _windowManager;
    private float _deviceX, _deviceY;
    private Vector2 _tilt;
    private readonly object _lock = new();

    public AndroidTiltSensor(Context context)
    {
        _manager = (SensorManager)context.GetSystemService(Context.SensorService);
        _windowManager = context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
        _sensor = _manager?.GetDefaultSensor(SensorType.Gravity);
        if (_sensor == null)
        {
            _sensor = _manager?.GetDefaultSensor(SensorType.Accelerometer);
            _filter = true;
        }
    }

    public bool IsAvailable => _sensor != null;

    public Vector2 Read()
    {
        lock (_lock)
            return _tilt;
    }

    public void Start()
    {
        if (_sensor != null)
            _manager.RegisterListener(this, _sensor, SensorDelay.Game);
    }

    public void Stop()
    {
        _manager?.UnregisterListener(this);
        lock (_lock)
            _tilt = Vector2.Zero;
    }

    public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
    {
    }

    public void OnSensorChanged(SensorEvent e)
    {
        if (e?.Values == null || e.Values.Count < 2)
            return;

        float x = e.Values[0], y = e.Values[1];
        if (_filter)
        {
            _deviceX += (x - _deviceX) * 0.2f;
            _deviceY += (y - _deviceY) * 0.2f;
        }
        else
        {
            _deviceX = x;
            _deviceY = y;
        }

        // Sensor axes follow the device's natural (portrait) orientation; at rest the sensor
        // reads "up". Convert to the landscape screen's right and up directions.
        float screenRight, screenUp;
#pragma warning disable CA1422
        var rotation = _windowManager?.DefaultDisplay?.Rotation ?? SurfaceOrientation.Rotation90;
#pragma warning restore CA1422
        switch (rotation)
        {
            case SurfaceOrientation.Rotation90: screenRight = -_deviceY; screenUp = _deviceX; break;
            case SurfaceOrientation.Rotation270: screenRight = _deviceY; screenUp = -_deviceX; break;
            case SurfaceOrientation.Rotation180: screenRight = -_deviceX; screenUp = -_deviceY; break;
            default: screenRight = _deviceX; screenUp = _deviceY; break;
        }

        // X: right-hand edge dipping is positive. Y: top edge tipping away is positive.
        var t = new Vector2(
            System.Math.Clamp(-screenRight / Gravity, -1f, 1f),
            System.Math.Clamp(-screenUp / Gravity, -1f, 1f));
        lock (_lock)
            _tilt = t;
    }
}
