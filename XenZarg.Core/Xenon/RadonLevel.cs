using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Xenon;

/// <summary>
/// LEVEL 4: on the yellow surface of planet Radon the commander fights on foot.
/// A Zorgon saucer drops Paratrons; those that land march underground with spears
/// raised. Kill 10-30 Paratrons depending on skill. 150 points each.
/// </summary>
public sealed class RadonLevel : XenonLevel
{
    public const int ParatronPoints = 150;
    public const int RocketPoints = 300;
    public const int GroundTop = 183;
    public const int GroundBottom = 199;
    public const int ManTop = 165;
    public const int SaucerY = 8;
    public const int TrooperLifetime = 600;
    public const float ManSpeed = 1.6f;

    public sealed class Paratron
    {
        public float X, Y, Speed;
        public int Phase;
    }

    public sealed class Trooper
    {
        public float X;
        public int Age;
        public bool Striking;
    }

    private readonly List<Paratron> _paratrons = new();
    private readonly List<Trooper> _troopers = new();
    private float _manX = 115;
    private bool _manFacingLeft;
    private float _saucerX = 108;
    private float _saucerDir = 1;
    private int _dropTimer = 60;
    private float _rocketX = -100;
    private float _rocketY;
    private bool _rocketActive;
    private int _rocketTimer = 400;
    private int _firingTimer;

    public RadonLevel(XenonWorld world) : base(world)
    {
        Required = RequiredKills(world.Session.Skill, world.Session.Cycle);
    }

    public override int Number => 4;

    /// <summary>Paratrons needed: 10 at skill 1 rising to 30 at skill 9, plus 5 per completed cycle.</summary>
    public static int RequiredKills(int skill, int cycle) =>
        10 + (int)Math.Round((Math.Clamp(skill, 1, 9) - 1) * 20 / 8.0) + cycle * 5;

    public int Required { get; }
    public int Kills { get; private set; }
    public IReadOnlyList<Paratron> Paratrons => _paratrons;
    public IReadOnlyList<Trooper> Troopers => _troopers;
    public float ManX { get => _manX; set => _manX = value; }

    public override float ShipX { get => _manX; set => _manX = Math.Clamp(value, 0, Canvas.Width - 10); }

    private static Sprite Man => Sprite.Of(SpriteData.Man);
    private static Sprite ManFiring => Sprite.Of(SpriteData.ManFiring);

    private Sprite ManSprite
    {
        get
        {
            var s = _firingTimer > 0 ? ManFiring : Man;
            return _manFacingLeft ? s.Mirror() : s;
        }
    }

    public override void Update()
    {
        int d = W.Difficulty;

        // The commander.
        float before = _manX;
        _manX = Math.Clamp(W.Controls.Steer(_manX, ManSpeed, false, 5), 0, Canvas.Width - Man.Width);
        if (_manX < before - 0.1f) _manFacingLeft = true;
        if (_manX > before + 0.1f) _manFacingLeft = false;
        if (_firingTimer > 0)
            _firingTimer--;
        if ((W.Controls.FirePressed || W.Controls.FireHeld) && W.Laser.TryFire(_manX + 4, ManTop - Laser.Height))
        {
            _firingTimer = 8;
            W.Sound.Play(Sfx.Shoot);
        }
        W.Laser.Update();

        // Saucer patrolling the top of the screen.
        _saucerX += _saucerDir * (0.8f + 0.08f * d);
        if (_saucerX < 0 || _saucerX > Canvas.Width - 24)
        {
            _saucerDir = -_saucerDir;
            _saucerX = Math.Clamp(_saucerX, 0, Canvas.Width - 24);
        }
        int maxFalling = 2 + d / 3;
        if (--_dropTimer <= 0 && _paratrons.Count < maxFalling)
        {
            _paratrons.Add(new Paratron
            {
                X = _saucerX + 6,
                Y = SaucerY + 12,
                Speed = 0.35f + 0.05f * d + (float)W.Rng.NextDouble() * 0.2f,
                Phase = W.Rng.Next(100),
            });
            _dropTimer = Math.Max(25, 80 - d * 5) + W.Rng.Next(30);
        }

        var manSprite = ManSprite;
        for (int i = _paratrons.Count - 1; i >= 0; i--)
        {
            var p = _paratrons[i];
            p.Y += p.Speed;
            p.X = Math.Clamp(p.X + MathF.Sin((W.Tick + p.Phase) * 0.05f) * 0.3f, 0, Canvas.Width - 11);
            var ps = ParatronSprite(p);
            if (W.Laser.Hits(ps, p.X, p.Y))
            {
                W.Laser.Active = false;
                _paratrons.RemoveAt(i);
                Kills++;
                W.Session.AddScore(ParatronPoints);
                W.AddBlast((int)p.X + 5, (int)p.Y + 10);
                W.Sound.Play(Sfx.EnemyHit);
                continue;
            }
            if (Sprite.Overlaps(ps, (int)p.X, (int)p.Y, manSprite, (int)_manX, ManTop))
            {
                _paratrons.RemoveAt(i);
                Kill((int)_manX + 5, ManTop + 9);
                continue;
            }
            if (p.Y + ps.Height >= GroundBottom - 2)
            {
                _paratrons.RemoveAt(i);
                _troopers.Add(new Trooper { X = Math.Clamp(p.X + 2, 0, Canvas.Width - 8) });
                W.Sound.Play(Sfx.Land);
            }
        }

        // Landed Paratrons march underground towards the commander, spears raised.
        for (int i = _troopers.Count - 1; i >= 0; i--)
        {
            var t = _troopers[i];
            t.Age++;
            float target = _manX + 1;
            float speed = 0.22f + 0.03f * d;
            if (Math.Abs(target - t.X) > speed)
                t.X += Math.Sign(target - t.X) * speed;
            t.Striking = Math.Abs(t.X + 4 - (_manX + 5)) < 4;
            if (t.Striking)
                Kill((int)_manX + 5, ManTop + 12);
            if (t.Age > TrooperLifetime)
                _troopers.RemoveAt(i);
        }

        UpdateRocket(d, manSprite);

        if (W.UpdateBombs(manSprite, (int)_manX, ManTop, GroundTop))
            Kill((int)_manX + 5, ManTop + 9);

        if (Kills >= Required)
            Completed = true;
    }

    private void UpdateRocket(int d, Sprite manSprite)
    {
        var rocket = Sprite.Of(SpriteData.Rocket);
        if (!_rocketActive)
        {
            if (--_rocketTimer <= 0)
            {
                _rocketActive = true;
                _rocketX = -rocket.Width;
                _rocketY = 50 + W.Rng.Next(50);
            }
            return;
        }
        _rocketX += 1.4f + 0.1f * d;
        if (W.Chance(0.02 + 0.004 * d) && _rocketX > 0 && _rocketX < Canvas.Width - 20)
            W.DropBomb(_rocketX + 9, _rocketY + 12, Sprite.Of(SpriteData.BombWhiteRed), 2f + 0.1f * d);
        if (W.Laser.Hits(rocket, _rocketX, _rocketY))
        {
            W.Laser.Active = false;
            W.Session.AddScore(RocketPoints);
            W.AddBlast((int)_rocketX + 9, (int)_rocketY + 6);
            W.Sound.Play(Sfx.EnemyHit);
            ResetRocket();
        }
        else if (_rocketX > Canvas.Width)
        {
            ResetRocket();
        }
    }

    private void ResetRocket()
    {
        _rocketActive = false;
        _rocketTimer = 500 + W.Rng.Next(400);
    }

    private Sprite ParatronSprite(Paratron p)
    {
        float sway = MathF.Cos((W.Tick + p.Phase) * 0.05f);
        if (Math.Abs(sway) < 0.6f)
            return Sprite.Of(SpriteData.Paratron);
        var tilt = Sprite.Of(SpriteData.ParatronTilt);
        return sway > 0 ? tilt : tilt.Mirror();
    }

    public override void Respawn()
    {
        base.Respawn();
        _manX = 115;
        _paratrons.Clear();
        _troopers.Clear();
        ResetRocket();
    }

    public override void Draw(Canvas c)
    {
        c.Fill(0, GroundTop, Canvas.Width, GroundBottom - GroundTop, OricColor.Yellow);

        var saucer = Sprite.Of(W.Tick / 6 % 2 == 0 ? SpriteData.Saucer1 : SpriteData.Saucer2);
        c.Draw(saucer, (int)_saucerX, SaucerY);
        if (_rocketActive)
            c.Draw(SpriteData.Rocket, (int)_rocketX, (int)_rocketY);

        foreach (var p in _paratrons)
            c.Draw(ParatronSprite(p), (int)p.X, (int)p.Y);

        foreach (var t in _troopers)
        {
            var s = Sprite.Of(t.Age / 12 % 2 == 0 ? SpriteData.Trooper1 : SpriteData.TrooperWalk);
            int x = (int)t.X;
            c.Draw(s, x, GroundBottom - s.Height);
            // Spear: red shaft with a white tip, thrust up when under the commander.
            int top = t.Striking ? ManTop + 6 : GroundTop - 5;
            c.Fill(x, top, 1, GroundTop - top, OricColor.Red);
            c.Plot(x, top - 1, OricColor.White);
        }

        if (!PlayerHit)
            c.Draw(ManSprite, (int)_manX, ManTop);
    }
}
