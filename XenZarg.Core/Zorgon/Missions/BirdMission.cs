using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon.Missions;

/// <summary>
/// The great yellow bird: jump to grab it as it swoops low and let it carry you up to the ledges.
/// Reaching the left side conjures a flashing bridge; cross it, catch the bird again from the
/// right and ride higher, until you can walk along the top to the magic stone.
/// </summary>
public sealed class BirdMission : PlatformMission
{
    public const int FloorTop = 189, MiddleTop = 119, UpperTop = 49;
    public const int LedgeWidth = 42;
    public const int BridgeX0 = 42, BridgeX1 = 197;
    public const int GoalX = 33 * 6 + 12;
    public const int RespawnTicks = 300;

    private static readonly string[][] BirdFrames =
    [
        SpriteData.ZBirdMissionBirdF0, SpriteData.ZBirdMissionBirdF1, SpriteData.ZBirdMissionBirdF2,
        SpriteData.ZBirdMissionBirdF3, SpriteData.ZBirdMissionBirdF4, SpriteData.ZBirdMissionBirdF5,
    ];

    private readonly object _birdCarrier = new();
    private float _birdX;
    private float _birdStartY;
    private int _birdSteps;
    private bool _birdActive;
    private int _respawn = 40;
    private bool _middleBridge, _upperBridge;
    private bool _highPass;
    private readonly List<(float x, float y)> _stones = new();
    private int _regrabDelay;

    public BirdMission(ZorgonWorld world) : base(world, 20, FloorTop)
    {
        Surfaces.Add(new Surface(0, 239, FloorTop));
        Surfaces.Add(new Surface(0, LedgeWidth - 1, MiddleTop));
        Surfaces.Add(new Surface(240 - LedgeWidth, 239, MiddleTop));
        Surfaces.Add(new Surface(0, LedgeWidth - 1, UpperTop));
        Surfaces.Add(new Surface(240 - LedgeWidth, 239, UpperTop));
        Hero.MinX = 8;
        Hero.MaxX = 232;
    }

    public override OricColor HudPaper => OricColor.Green;
    public override OricColor HudInk => OricColor.Black;
    public override int StartBonus => 7000;

    public bool MiddleBridge => _middleBridge;
    public bool UpperBridge => _upperBridge;
    public bool BirdActive => _birdActive;
    public (float x, float y) Bird => (_birdX, BirdY);

    /// <summary>The bird's height along its path: level, a dip of 7 rows, a long climb of 57, level again.</summary>
    public float BirdY
    {
        get
        {
            float s = _birdSteps;
            if (s < 10) return _birdStartY;
            if (s < 30) return _birdStartY + (s - 10) * 7 / 20f;
            if (s < 130) return _birdStartY + 7 - (s - 30) * 57 / 100f;
            return _birdStartY - 50;
        }
    }

    private List<Surface> CurrentSurfaces()
    {
        var list = new List<Surface>(Surfaces);
        if (_middleBridge)
            list.Add(new Surface(BridgeX0, BridgeX1, MiddleTop));
        if (_upperBridge)
            list.Add(new Surface(BridgeX0, BridgeX1, UpperTop));
        return list;
    }

    public override void Update()
    {
        UpdateBird();
        bool wasRiding = Hero.Carrier == _birdCarrier;
        UpdateHero(CurrentSurfaces());
        if (wasRiding && Hero.Carrier != _birdCarrier)
            _regrabDelay = 40;
        if (_regrabDelay > 0)
            _regrabDelay--;

        if (Hero.Carrier == _birdCarrier)
        {
            Hero.Carry(_birdX + 12 + 11.5f, BirdY + 13 + Hero.Height);
            if (!_middleBridge && Hero.Top <= 98 && Hero.X <= 6 * 6 + 12)
            {
                _middleBridge = true;
                W.Sound.Play(Sfx.Bridge);
            }
            if (_highPass && !_upperBridge && Hero.Top <= 28 && Hero.X <= 6 * 6 + 12)
            {
                _upperBridge = true;
                W.Sound.Play(Sfx.Bridge);
            }
            if (!_birdActive)
                Hero.Release();
        }

        TryCatchBird();

        // The middle bridge melts away once you are across.
        if (_middleBridge && !_highPass && Hero.Current == Hero.State.Standing && Hero.Feet == MiddleTop && Hero.X >= 240 - LedgeWidth + 4)
        {
            _middleBridge = false;
            _highPass = true;
        }

        UpdateStones();

        if (_upperBridge && Hero.Current == Hero.State.Standing && Hero.Feet == UpperTop && Hero.X >= GoalX)
            Completed = true;
    }

    private void UpdateBird()
    {
        if (!_birdActive)
        {
            if (--_respawn <= 0)
            {
                _birdActive = true;
                _birdX = 210;
                // Once the middle bridge has appeared the bird flies its high pass.
                _birdStartY = _middleBridge || _highPass ? 57 : 126;
                _birdSteps = 0;
            }
            return;
        }
        float step = ZorgonWorld.PerTick(10f);
        _birdX -= step;
        _birdSteps = (int)((210 - _birdX));
        if (_birdX < -30)
        {
            _birdActive = false;
            _respawn = RespawnTicks;
        }
    }

    private void TryCatchBird()
    {
        if (!_birdActive || !Hero.Airborne || Hero.Carrier != null || _regrabDelay > 0)
            return;
        // As on the Oric: the hero's head must be 6-16 rows below the top of the bird.
        float rel = Hero.Top - BirdY;
        if (rel >= 6 && rel <= 16 && Math.Abs(Hero.X - (_birdX + 17)) <= 10)
        {
            Hero.Ride(_birdCarrier, _birdX + 12 + 11.5f, BirdY + 13 + Hero.Height);
            W.Sound.Play(Sfx.Rope);
        }
    }

    private void UpdateStones()
    {
        // Stones rain down; none while you cling to the bird.
        if (Hero.Carrier != _birdCarrier && W.Chance(0.008 + 0.004 * W.Hardness))
            _stones.Add((48 + W.Rng.Next(139), 0));
        var stone = Sprite.Of(SpriteData.ZStone);
        for (int i = _stones.Count - 1; i >= 0; i--)
        {
            var (x, y) = _stones[i];
            y += ZorgonWorld.PerTick(50f) * 0.6f;
            _stones[i] = (x, y);
            if (Hero.Touches(stone, (int)x, (int)y))
            {
                Kill();
                _stones.RemoveAt(i);
            }
            else if (y > FloorTop)
            {
                _stones.RemoveAt(i);
            }
        }
    }

    public override void Draw(Canvas c)
    {
        c.Fill(0, FloorTop, Canvas.Width, 200 - FloorTop, OricColor.Magenta);
        foreach (int top in new[] { MiddleTop, UpperTop })
        {
            c.Fill(0, top, LedgeWidth, 6, OricColor.Magenta);
            c.Fill(240 - LedgeWidth, top, LedgeWidth, 6, OricColor.Magenta);
        }
        if (!_upperBridge || !Completed)
            c.Draw(SpriteData.ZBridgePartBlock, 210, 43);

        var flash = (W.Tick / 6 % 3) switch { 0 => OricColor.White, 1 => OricColor.Green, _ => OricColor.Blue };
        if (_middleBridge)
            c.Fill(BridgeX0, MiddleTop, BridgeX1 - BridgeX0 + 1, 6, flash);
        if (_upperBridge)
            c.Fill(BridgeX0, UpperTop, BridgeX1 - BridgeX0 + 1, 6, flash);

        foreach (var (x, y) in _stones)
            c.Fill((int)x, (int)y, 1, 5, OricColor.White);

        if (_birdActive)
            c.Draw(BirdFrames[W.Tick / 6 % BirdFrames.Length], (int)_birdX, (int)BirdY);
        Hero.Draw(c);
    }
}
