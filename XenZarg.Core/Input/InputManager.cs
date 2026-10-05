using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using XenZarg.Core.Platform;

namespace XenZarg.Core.Input;

/// <summary>
/// Gathers keyboard (Z / X / SPACE and cursor keys), mouse, touch, tilt and game pad
/// into a single per-frame view used by every scene.
/// </summary>
public sealed class InputManager : IControls
{
    /// <summary>Taps above this virtual y are treated as touching the FUEL gauge.</summary>
    public const int FuelZoneHeight = 14;

    private readonly ITiltSensor _tilt;
    private readonly TiltMapper _tiltMapper = new();
    private readonly bool _isMobile;
    private KeyboardState _prevKeys;
    private MouseState _prevMouse;
    private GamePadState _prevPad;
    private bool _mouseActive;
    private readonly List<Vector2> _taps = new();
    private readonly List<char> _typed = new();

    public InputManager(ITiltSensor tilt, bool isMobile)
    {
        _tilt = tilt ?? new NullTiltSensor();
        _isMobile = isMobile;
        if (_isMobile)
            TouchPanel.EnabledGestures = GestureType.None;
    }

    public bool IsMobile => _isMobile;
    public bool TiltAvailable => _tilt.IsAvailable;

    /// <summary>Horizontal steer -1 (left) .. 1 (right) from keys, pad or tilt.</summary>
    public float MoveX { get; private set; }

    /// <summary>Vertical steer -1 (up) .. 1 (down) from keys, pad or tilt.</summary>
    public float MoveY { get; private set; }

    /// <summary>Mouse position in virtual pixels while the mouse is the active device.</summary>
    public Vector2? Pointer { get; private set; }

    public bool FirePressed { get; private set; }
    public bool FireHeld { get; private set; }
    public bool DisengagePressed { get; private set; }
    public bool BackPressed { get; private set; }
    public bool ConfirmPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool LeftPressed { get; private set; }
    public bool RightPressed { get; private set; }
    public bool UpPressed { get; private set; }
    public bool DownPressed { get; private set; }
    public bool SkillPressed { get; private set; }
    public bool InstructionsPressed { get; private set; }
    public bool VolumePressed { get; private set; }

    /// <summary>New touches this frame, in virtual pixels.</summary>
    public IReadOnlyList<Vector2> Taps => _taps;

    /// <summary>Letters / digits typed this frame (desktop name entry).</summary>
    public IReadOnlyList<char> Typed => _typed;

    public void CalibrateTilt() => _tiltMapper.Calibrate();

    public void Update(Func<Vector2, Vector2> toVirtual, bool isActive)
    {
        _taps.Clear();
        _typed.Clear();

        var keys = Keyboard.GetState();
        var pad = GamePad.GetState(PlayerIndex.One);
        bool Down(Keys k) => keys.IsKeyDown(k);
        bool Hit(Keys k) => keys.IsKeyDown(k) && !_prevKeys.IsKeyDown(k);
        bool PadHit(Buttons b) => pad.IsButtonDown(b) && !_prevPad.IsButtonDown(b);

        // --- digital steering: Z / X as specified, cursor keys as on the Oric ---
        float kx = 0, ky = 0;
        bool left = Down(Keys.Z) || Down(Keys.Left);
        bool right = Down(Keys.X) || Down(Keys.Right);
        if (left) kx -= 1;
        if (right) kx += 1;
        if (Down(Keys.Up)) ky -= 1;
        if (Down(Keys.Down)) ky += 1;
        if (ky == 0)
        {
            // Z / X double as up / down where a screen only moves vertically.
            if (Down(Keys.Z)) ky -= 1;
            if (Down(Keys.X)) ky += 1;
        }

        var stick = pad.ThumbSticks.Left;
        if (Math.Abs(stick.X) > 0.2f) kx = stick.X;
        if (Math.Abs(stick.Y) > 0.2f) ky = -stick.Y;
        if (pad.IsButtonDown(Buttons.DPadLeft)) kx = -1;
        if (pad.IsButtonDown(Buttons.DPadRight)) kx = 1;
        if (pad.IsButtonDown(Buttons.DPadUp)) ky = -1;
        if (pad.IsButtonDown(Buttons.DPadDown)) ky = 1;

        bool keyboardSteering = kx != 0 || ky != 0;

        // --- tilt (mobile) ---
        float tx = 0, ty = 0;
        if (_tilt.IsAvailable)
        {
            var t = _tiltMapper.Map(_tilt.Read());
            tx = t.X;
            ty = -t.Y; // tipping the top edge away moves up the screen
        }

        MoveX = keyboardSteering ? kx : tx;
        MoveY = keyboardSteering ? ky : ty;

        LeftPressed = Hit(Keys.Z) || Hit(Keys.Left) || PadHit(Buttons.DPadLeft);
        RightPressed = Hit(Keys.X) || Hit(Keys.Right) || PadHit(Buttons.DPadRight);
        UpPressed = Hit(Keys.Up) || PadHit(Buttons.DPadUp);
        DownPressed = Hit(Keys.Down) || PadHit(Buttons.DPadDown);

        bool fireKey = Down(Keys.Space) || pad.IsButtonDown(Buttons.A);
        bool fireHit = Hit(Keys.Space) || PadHit(Buttons.A);
        bool disengage = Hit(Keys.Up) || Hit(Keys.R) || PadHit(Buttons.B) || PadHit(Buttons.Y);
        bool confirm = Hit(Keys.Space) || Hit(Keys.Enter) || PadHit(Buttons.A) || PadHit(Buttons.Start);
        BackPressed = Hit(Keys.Escape) || PadHit(Buttons.Back);
        PausePressed = Hit(Keys.P) || PadHit(Buttons.Start);
        SkillPressed = Hit(Keys.S);
        InstructionsPressed = Hit(Keys.I);
        VolumePressed = Hit(Keys.V);

        // --- mouse (desktop only) ---
        Pointer = null;
        if (!_isMobile)
        {
            var mouse = Mouse.GetState();
            bool moved = mouse.X != _prevMouse.X || mouse.Y != _prevMouse.Y;
            if (moved && isActive)
                _mouseActive = true;
            if (keyboardSteering)
                _mouseActive = false;

            var v = toVirtual(new Vector2(mouse.X, mouse.Y));
            bool inside = v.X >= 0 && v.Y >= 0 && v.X < Graphics.Canvas.Width && v.Y < Graphics.Canvas.Height;
            if (_mouseActive && inside && isActive)
                Pointer = v;

            bool clickL = isActive && inside && mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
            bool clickR = isActive && inside && mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;
            if (clickL && v.Y < FuelZoneHeight)
                disengage = true;
            else if (clickL)
            {
                fireHit = true;
                confirm = true;
                _taps.Add(v);
            }
            if (isActive && inside && mouse.LeftButton == ButtonState.Pressed)
                fireKey = true;
            if (clickR)
                disengage = true;
            _prevMouse = mouse;
        }

        // --- touch ---
        bool anyTouch = false;
        var touches = TouchPanel.GetState();
        int pressedCount = 0;
        foreach (var t in touches)
        {
            if (t.State == TouchLocationState.Pressed || t.State == TouchLocationState.Moved)
                anyTouch = true;
            if (t.State != TouchLocationState.Pressed)
                continue;
            pressedCount++;
            var v = toVirtual(t.Position);
            _taps.Add(v);
            if (v.Y < FuelZoneHeight)
                disengage = true;
            else
                fireHit = true;
            confirm = true;
        }
        if (touches.Count >= 3 && pressedCount > 0)
            PausePressed = true;
        if (anyTouch)
            fireKey = true;

        // --- typed characters for name entry ---
        foreach (var k in keys.GetPressedKeys())
        {
            if (_prevKeys.IsKeyDown(k))
                continue;
            if (k >= Keys.A && k <= Keys.Z)
                _typed.Add((char)('A' + (k - Keys.A)));
            else if (k >= Keys.D0 && k <= Keys.D9)
                _typed.Add((char)('0' + (k - Keys.D0)));
            else if (k == Keys.Back)
                _typed.Add('\b');
        }

        FireHeld = fireKey;
        FirePressed = fireHit;
        DisengagePressed = disengage;
        ConfirmPressed = confirm;

        _prevKeys = keys;
        _prevPad = pad;
    }

    /// <summary>
    /// Moves a coordinate towards the player's wish: follows the mouse when it is active,
    /// otherwise uses the analog / digital axis. Never exceeds <paramref name="speed"/> per tick.
    /// </summary>
    public float Steer(float position, float speed, bool vertical, float pointerOffset = 0)
    {
        if (Pointer is Vector2 p)
        {
            float target = (vertical ? p.Y : p.X) - pointerOffset;
            float delta = target - position;
            if (Math.Abs(delta) <= speed)
                return target;
            return position + Math.Sign(delta) * speed;
        }
        float axis = vertical ? MoveY : MoveX;
        return position + axis * speed;
    }
}
