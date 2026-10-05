using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon.Missions;

/// <summary>
/// Inside the castle: Zorgon stands on a stepped platform beside Princess Roz. Jump to touch the
/// trigger under the armed ledge to fire its cannon; three hits from the left cannon crumble the
/// platform and Zorgon falls. Ants patrol the floor stinging upwards.
/// </summary>
public sealed class KingScreen : PlatformMission
{
    public const int FloorTop = 189;
    public const int SensorRow = 150;
    public const int LeftSensorX = 30, RightSensorX = 212;
    public const int HitsNeeded = 3;
    public const int KingX = 98, KingY = 20;
    public const int PlatformX0 = 90, PlatformX1 = 149, PlatformY0 = 48, PlatformY1 = 77;

    private static readonly (int x0, int x1, int y, OricColor c)[] Steps =
    [
        (90, 149, 48, OricColor.Red),
        (96, 143, 54, OricColor.Green),
        (102, 137, 60, OricColor.Magenta),
        (108, 131, 66, OricColor.Cyan),
        (114, 125, 72, OricColor.White),
    ];

    private static readonly string[][] AntFrames = [SpriteData.ZAntF0, SpriteData.ZAntF1, SpriteData.ZAntF2];

    public sealed class CannonShot
    {
        public float X, Y;
        public bool FromLeft;
    }

    public sealed class Ant
    {
        public float X;
        public int StingTimer;
        public int Cycle;
    }

    private readonly List<CannonShot> _shots = new();
    private readonly List<Ant> _ants = new();
    private bool _rightArmed = true;
    private int _hits;
    private float _crumble;
    private float _kingFall;
    private int _endTimer;

    public KingScreen(ZorgonWorld world) : base(world, 12, FloorTop)
    {
        Surfaces.Add(new Surface(0, 239, FloorTop));
        Hero.MinX = 8;
        Hero.MaxX = 232;
        foreach (int col in new[] { 5, 16, 27 })
            _ants.Add(new Ant { X = col * 6, StingTimer = 40 + col * 3 });
    }

    public override OricColor HudPaper => OricColor.Yellow;
    public override OricColor HudInk => OricColor.Blue;
    public override int StartBonus => 9000;
    public override bool AwardsBonus => false;

    public int Hits => _hits;
    public bool RightArmed => _rightArmed;
    public bool KingFalling => _hits >= HitsNeeded;
    public IReadOnlyList<CannonShot> Shots => _shots;

    public override void Update()
    {
        if (_endTimer > 0)
        {
            if (++_endTimer > 100)
                Completed = true;
            return;
        }

        UpdateHero();
        TryFire();
        UpdateShots();
        UpdateAnts();

        if (KingFalling)
        {
            if (_crumble < Steps.Length)
                _crumble += ZorgonWorld.PerTick(5.3f);
            else
            {
                _kingFall += ZorgonWorld.PerTick(83f) / 2;
                if (KingY + _kingFall > 230)
                {
                    _endTimer = 1;
                    W.Sound.Play(Sfx.Victory);
                }
            }
        }
    }

    private void TryFire()
    {
        if (!Hero.Airborne || Hero.Top > SensorRow)
            return;
        bool left = Hero.X <= LeftSensorX;
        bool right = Hero.X >= RightSensorX;
        if ((left && !_rightArmed) || (right && _rightArmed))
        {
            _shots.Add(left
                ? new CannonShot { X = 46, Y = 118, FromLeft = true }
                : new CannonShot { X = 190, Y = 118, FromLeft = false });
            _rightArmed = !_rightArmed;
            W.Sound.Play(Sfx.Bridge);
        }
    }

    private void UpdateShots()
    {
        float step = ZorgonWorld.PerTick(66f) * 0.5f;
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            s.Y -= step;
            s.X += s.FromLeft ? step : -step;
            bool atPlatform = s.Y <= PlatformY1 && s.X >= PlatformX0 - 6 && s.X <= PlatformX1;
            if (atPlatform || s.Y < 0)
            {
                _shots.RemoveAt(i);
                if (atPlatform && s.FromLeft && _hits < HitsNeeded)
                {
                    // Only the left cannon's hits count, as on the Oric.
                    _hits++;
                    W.Sound.Play(Sfx.EnemyHit);
                    if (_hits == HitsNeeded)
                        W.Sound.Play(Sfx.Fall);
                }
            }
        }
    }

    private void UpdateAnts()
    {
        float speed = ZorgonWorld.PerTick(16.7f);
        foreach (var a in _ants)
        {
            a.X -= speed;
            if (a.X < 6)
                a.X = 35 * 6;
            if (--a.StingTimer <= 0)
            {
                a.Cycle++;
                a.StingTimer = Math.Max(50, 110 - W.Hardness * 6);
            }
            if (Stinging(a) && Hero.TouchesRect((int)a.X + 15, 181, 1, 8))
                Kill();
        }
    }

    /// <summary>Every other cycle the ant raises its sting for a moment.</summary>
    private static bool Stinging(Ant a) => a.Cycle % 2 == 1 && a.StingTimer > 30;

    public override void Draw(Canvas c)
    {
        c.Draw(SpriteData.KingBackground, 0, 0);

        int remaining = Steps.Length - (int)Math.Min(Steps.Length, _crumble);
        for (int i = 0; i < remaining; i++)
        {
            var (x0, x1, y, col) = Steps[i];
            c.Fill(x0, y, x1 - x0 + 1, 6, col);
        }

        int ky = KingY + (int)_kingFall;
        if (ky < 200)
            c.Draw(W.Tick / 15 % 2 == 0 ? SpriteData.ZZorgonKingF0 : SpriteData.ZZorgonKingF1, KingX, ky);
        c.Draw(SpriteData.ZPrincessRoz, 137, 20);
        if (!KingFalling && W.Tick / 20 % 2 == 0)
            c.Draw(SpriteData.ZHelpBubble, 144, 5);

        // The armed trigger pointer flashes.
        if (W.Tick / 10 % 2 == 0)
        {
            int px = _rightArmed ? 224 : 12;
            c.Fill(px, 134, 1, 8, OricColor.White);
            c.Fill(px - 2, 140, 5, 1, OricColor.Yellow);
            c.Fill(px - 1, 141, 3, 1, OricColor.Yellow);
            c.Plot(px, 142, OricColor.Yellow);
        }

        foreach (var s in _shots)
            c.Draw(s.FromLeft ? SpriteData.ZCannonShotLeftF0 : SpriteData.ZCannonShotRightF0, (int)s.X - 6, (int)s.Y);

        foreach (var a in _ants)
        {
            c.DrawTinted(Sprite.Of(AntFrames[W.Tick / 6 % AntFrames.Length]), (int)a.X, 190, OricColor.Cyan);
            if (Stinging(a))
                c.Fill((int)a.X + 15, 181, 1, 8, OricColor.Cyan);
        }

        if (_endTimer > 0)
            c.Fill(0, 0, Canvas.Width, 200, OricColor.Black);
        Hero.Draw(c);
    }
}
