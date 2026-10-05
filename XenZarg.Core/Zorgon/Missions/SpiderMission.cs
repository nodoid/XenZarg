using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon.Missions;

/// <summary>
/// The spider's cavern: catch the swinging rod over the river, ride the lift up, jump to hit the
/// button over the ledge (which frees the caged monster and reverses the lift), then make it all
/// the way back to where you started.
/// </summary>
public sealed class SpiderMission : PlatformMission
{
    public const int FloorTop = 189;
    public const int LedgeTop = 76;
    public const int RopeTop = 139, RopeBottom = 150;
    public const int RopeLeft = 77, RopeRight = 132;
    public const int HangFeet = 146 + Hero.Height;
    public const int ButtonX0 = 58, ButtonX1 = 80, ButtonRow = 36;
    public const int HomeX = 36;
    public const int SpiderX = 179;
    public const int SpiderHigh = 94, SpiderLow = 152;
    public const int MonsterStartX = 12, MonsterY = 49, MonsterLimitX = 174;

    private readonly Lift _lift;
    private readonly Surface _river = new(72, 137, FloorTop + 3, Deadly: true);
    private float _ropeX = RopeLeft;
    private int _ropeDir = 1;
    private readonly float _ropeSpeed = ZorgonWorld.PerTick(12.5f);
    private float _spiderY = SpiderHigh;
    private int _spiderDir = 1;
    private readonly float _spiderSpeed;
    private float _flyerX = 40, _flyerY = 100;
    private int _flyerDirX = 1, _flyerDirY = 1;
    private float _stoneX, _stoneY;
    private bool _stoneFalling;
    private float _monsterX = MonsterStartX;
    private readonly object _rope = new();
    private int _regrabDelay;

    public SpiderMission(ZorgonWorld world) : base(world, 12, FloorTop)
    {
        Surfaces.Add(new Surface(6, 71, FloorTop));
        Surfaces.Add(new Surface(138, 209, FloorTop));
        Surfaces.Add(new Surface(0, 209, LedgeTop));
        Surfaces.Add(new Surface(72, 137, 133));
        Surfaces.Add(_river);
        Hero.MinX = 8;
        int d = world.Hardness;
        _lift = new Lift(Lift.Lowest, ZorgonWorld.PerTick(12.5f + d), -1);
        _spiderSpeed = ZorgonWorld.PerTick(2.5f + d * 0.6f);
    }

    public override OricColor HudPaper => OricColor.Cyan;
    public override OricColor HudInk => OricColor.Black;
    public override int StartBonus => 7900;

    public bool ButtonPressed { get; private set; }
    public float RopeX => _ropeX;
    public Lift Lift => _lift;

    private IReadOnlyList<Surface> AllSurfaces()
    {
        var list = new List<Surface>(Surfaces) { _lift.Surface };
        return list;
    }

    public override void Update()
    {
        _lift.Update();
        UpdateRope();
        UpdateSpider();
        UpdateFlyer();
        if (ButtonPressed)
            UpdateMonster();

        bool wasOnRope = Hero.Carrier == _rope;
        bool landed = UpdateHero(AllSurfaces());
        if (wasOnRope && Hero.Carrier != _rope)
            _regrabDelay = 30;
        if (_regrabDelay > 0)
            _regrabDelay--;
        _lift.Carry(Hero, landed);
        if (Hero.Carrier == _lift && _lift.Wrapped)
            Hero.Die(W.Sound);

        CatchRope();
        if (Hero.Carrier == _rope)
            Hero.Carry(_ropeX, HangFeet);

        // The icicles: jumping too high is fatal.
        if (Hero.Top < 20 && Hero.X > 60)
            Kill();

        if (!ButtonPressed && Hero.Airborne && Hero.Top <= ButtonRow && Hero.X >= ButtonX0 && Hero.X <= ButtonX1 && Hero.Feet < LedgeTop + 2)
        {
            ButtonPressed = true;
            _lift.Direction = 1;
            W.Sound.Play(Sfx.Pickup);
        }

        if (ButtonPressed && Hero.Current == Hero.State.Standing && Hero.Feet == FloorTop && Hero.X <= HomeX)
            Completed = true;
    }

    private void UpdateRope()
    {
        _ropeX += _ropeDir * _ropeSpeed;
        if (_ropeX >= RopeRight) { _ropeX = RopeRight; _ropeDir = -1; }
        if (_ropeX <= RopeLeft) { _ropeX = RopeLeft; _ropeDir = 1; }
    }

    private void CatchRope()
    {
        if (!Hero.Airborne || Hero.Carrier != null || _regrabDelay > 0)
            return;
        if (Hero.Top <= 156 && Hero.Feet >= RopeTop && Math.Abs(Hero.X - _ropeX) <= 8)
        {
            Hero.Ride(_rope, _ropeX, HangFeet);
            W.Sound.Play(Sfx.Rope);
        }
    }

    private void UpdateSpider()
    {
        _spiderY += _spiderDir * _spiderSpeed;
        if (_spiderY >= SpiderLow) { _spiderY = SpiderLow; _spiderDir = -1; }
        if (_spiderY <= SpiderHigh) { _spiderY = SpiderHigh; _spiderDir = 1; }
        if (Hero.Touches(SpiderSprite, SpiderX, (int)_spiderY))
            Kill();
    }

    private Sprite SpiderSprite => Sprite.Of((W.Tick / 10 % 3) switch
    {
        0 => SpriteData.ZSpiderF0,
        1 => SpriteData.ZSpiderF1,
        _ => SpriteData.ZSpiderF2,
    });

    private Sprite FlyerSprite => Sprite.Of((W.Tick / 8 % 3) switch
    {
        0 => SpriteData.ZSpiderLevelBirdAF0,
        1 => SpriteData.ZSpiderLevelBirdAF1,
        _ => SpriteData.ZSpiderLevelBirdAF2,
    });

    private void UpdateFlyer()
    {
        _flyerX += _flyerDirX * ZorgonWorld.PerTick(10f);
        _flyerY += _flyerDirY * ZorgonWorld.PerTick(5f);
        if (_flyerX > 150 || _flyerX < 6) _flyerDirX = -_flyerDirX;
        if (_flyerY > 116 || _flyerY < 95) _flyerDirY = -_flyerDirY;

        if (!_stoneFalling && W.Chance(0.02 + 0.004 * W.Hardness))
        {
            _stoneFalling = true;
            _stoneX = _flyerX + 15;
            _stoneY = _flyerY + 14;
        }
        if (_stoneFalling)
        {
            _stoneY += ZorgonWorld.PerTick(25f);
            var stone = Sprite.Of(SpriteData.ZStone);
            if (Hero.Touches(stone, (int)_stoneX, (int)_stoneY))
            {
                Kill();
                _stoneFalling = false;
            }
            // The floating platform shields the rope; floors stop stones.
            bool onPlatform = _stoneX >= 72 && _stoneX <= 137 && _stoneY + 5 >= 133 && _stoneY < 138;
            if (onPlatform || _stoneY + 5 >= FloorTop)
                _stoneFalling = false;
        }
        if (Hero.Touches(FlyerSprite, (int)_flyerX, (int)_flyerY))
            Kill();
    }

    private void UpdateMonster()
    {
        if (_monsterX < MonsterLimitX)
            _monsterX += ZorgonWorld.PerTick(7f + W.Hardness);
        if (Hero.Touches(MonsterSprite, (int)_monsterX, MonsterY))
            Kill();
    }

    private Sprite MonsterSprite => Sprite.Of((W.Tick / 12 % 3) switch
    {
        0 => SpriteData.ZCageMonsterF0,
        1 => SpriteData.ZCageMonsterF1,
        _ => SpriteData.ZCageMonsterF2,
    });

    public override void Draw(Canvas c)
    {
        c.Draw(SpriteData.SpiderBackground, 0, 0);
        if (ButtonPressed)
        {
            // The cage house and its lasers vanish, the bridge part materialises below.
            c.Fill(0, 7, 49, 69, OricColor.Black);
            c.Draw(SpriteData.ZBridgePartBlock, 0, 183);
            c.Draw(MonsterSprite, (int)_monsterX, MonsterY);
        }

        // River stripes alternate yellow / red, swapping as the water flows.
        int phase = W.Tick / 4 % 2;
        for (int y = 192; y < 200; y++)
            c.Fill(72, y, 66, 1, (y + phase) % 2 == 0 ? OricColor.Yellow : OricColor.Red);

        // Rod hanging from the floating platform.
        c.Fill((int)_ropeX, RopeTop, 1, RopeBottom - RopeTop + 1, OricColor.Cyan);

        // Spider on its thread from the web.
        c.Fill(SpiderX + 18, 96, 1, Math.Max(0, (int)_spiderY - 96), OricColor.White);
        c.Draw(SpiderSprite, SpiderX, (int)_spiderY);

        c.Draw(FlyerSprite, (int)_flyerX, (int)_flyerY);
        if (_stoneFalling)
            c.Draw(SpriteData.ZStone, (int)_stoneX, (int)_stoneY);

        _lift.Draw(c);
        Hero.Draw(c);
    }
}
