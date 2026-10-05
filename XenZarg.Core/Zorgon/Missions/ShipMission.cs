using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon.Missions;

/// <summary>
/// Deep space: the ship sits at the left and moves only up and down, firing growing beams.
/// Ten kills bring the mothership; hitting it teleports the magic stone aboard.
/// </summary>
public sealed class ShipMission : ZorgonMission
{
    public const int ShipX = 0;
    public const int ShipTop = 1, ShipBottom = 174;
    public const int MaterialiseTicks = 80;
    public const int BeamStartX = 30, BeamMaxLength = 144, BeamLife = 27, MaxBeams = 3;
    public const int MaxAliens = 4;
    public const int AlienPoints = 150;
    public const int KillsForMothership = 10;
    public const int GroundY = 190;

    /// <summary>Alien "updates" per game tick (13.3 Hz in the original at the common speed).</summary>
    private static readonly float UpdateRate = ZorgonWorld.PerTick(13.3f);

    public sealed class Beam
    {
        public int Y;
        public float Head;
        public int Age;
        public OricColor Color;
        public float Tail => Math.Max(BeamStartX, Head - BeamMaxLength);
    }

    public sealed class Alien
    {
        public float X, Y, Vy;
        public float Life;
        public int Phase;
    }

    public sealed class Missile
    {
        public float X, Y;
    }

    private static readonly string[][] AlienFrames = [SpriteData.ZAlienF0, SpriteData.ZAlienF1, SpriteData.ZAlienF2];
    private static readonly string[][] MotherFrames = [SpriteData.ZMothershipF0, SpriteData.ZMothershipF1, SpriteData.ZMothershipF2];
    private static readonly string[][] CubeFrames =
    [
        SpriteData.ZSpinningCubeF0, SpriteData.ZSpinningCubeF1, SpriteData.ZSpinningCubeF2,
        SpriteData.ZSpinningCubeF3, SpriteData.ZSpinningCubeF4, SpriteData.ZSpinningCubeF5,
    ];

    private readonly List<Beam> _beams = new();
    private readonly List<Alien> _aliens = new();
    private readonly List<Missile> _missiles = new();
    private float _shipY = 90;
    private int _materialise = MaterialiseTicks;
    private int _beamColor = 7;
    private int _kills;
    private bool _motherActive;
    private float _motherX, _motherY;
    private int _motherLife;
    private int _motherDir = 1;
    private bool _cubeActive;
    private float _cubeX, _cubeY;
    private int _dying;
    private int _won;

    public ShipMission(ZorgonWorld world) : base(world)
    {
    }

    public override OricColor HudPaper => OricColor.Red;
    public override OricColor HudInk => OricColor.Yellow;
    public override int StartBonus => 1000;
    public override bool BonusCountsDown => false;

    public float ShipY { get => _shipY; set => _shipY = value; }
    public int Kills => _kills;
    public bool MothershipActive => _motherActive;
    public (float x, float y) Mothership => (_motherX, _motherY);
    public IReadOnlyList<Alien> Aliens => _aliens;
    public IReadOnlyList<Beam> Beams => _beams;
    public bool Materialising => _materialise > 0;
    public bool Dying => _dying > 0;

    private static Sprite ShipSprite => Sprite.Of(SpriteData.ZPlayerShip);

    public override void Update()
    {
        if (_won > 0)
        {
            if (++_won > 90)
                Completed = true;
            return;
        }
        if (_dying > 0)
        {
            if (++_dying > 60)
                Failed = true;
            return;
        }
        if (_materialise > 0)
        {
            _materialise--;
            if (_materialise == MaterialiseTicks - 1)
                W.Sound.Play(Sfx.Refuel);
            return;
        }

        // Up and down only: tilt forward/back, cursor keys, Z / X or the mouse.
        _shipY = Math.Clamp(W.Controls.Steer(_shipY, ZorgonWorld.PerTick(44f), true, 8), ShipTop, ShipBottom);
        if (W.Controls.FirePressed && _beams.Count < MaxBeams)
        {
            _beams.Add(new Beam { Y = (int)_shipY + 9, Head = BeamStartX, Color = (OricColor)_beamColor });
            _beamColor = _beamColor <= 1 ? 7 : _beamColor - 1;
            W.Sound.Play(Sfx.Shoot);
        }

        UpdateBeams();
        UpdateAliens();
        UpdateCube();
        UpdateMothership();
        UpdateMissiles();
    }

    private void UpdateBeams()
    {
        for (int i = _beams.Count - 1; i >= 0; i--)
        {
            var b = _beams[i];
            b.Head += ZorgonWorld.PerTick(190f);
            if (++b.Age > BeamLife || b.Tail > Canvas.Width)
                _beams.RemoveAt(i);
        }
    }

    private bool BeamHits(Sprite s, float x, float y, out Beam hit)
    {
        foreach (var b in _beams)
        {
            if (Sprite.OverlapsRect(s, (int)x, (int)y, (int)b.Tail, b.Y, (int)(b.Head - b.Tail), 1))
            {
                hit = b;
                return true;
            }
        }
        hit = null;
        return false;
    }

    private void UpdateAliens()
    {
        int d = W.Hardness;
        if (_aliens.Count < MaxAliens && W.Chance(8 / 256.0 * UpdateRate * (1 + d * 0.15)))
        {
            _aliens.Add(new Alien
            {
                X = 210,
                Y = 1 + 6 * W.Rng.Next(0, 28),
                Vy = (W.Rng.Next(2) == 0 ? -1 : 1) * 2f / 3f,
                Life = 106,
                Phase = W.Rng.Next(3),
            });
        }

        for (int i = _aliens.Count - 1; i >= 0; i--)
        {
            var a = _aliens[i];
            a.X -= 2 * UpdateRate * (1 + d * 0.1f);
            a.Y += a.Vy * UpdateRate * 3;
            if (a.Y < 1 || a.Y > 172)
            {
                a.Vy = -a.Vy;
                a.Y = Math.Clamp(a.Y, 1, 172);
            }
            a.Life -= UpdateRate;
            var sprite = AlienSprite(a);
            if (W.Chance(UpdateRate / 40.0 * (1 + d * 0.2)))
                _missiles.Add(new Missile { X = a.X, Y = a.Y + 6 });

            if (BeamHits(sprite, a.X, a.Y, out var beam))
            {
                _beams.Remove(beam);
                _aliens.RemoveAt(i);
                _kills++;
                W.Session.AddScore(AlienPoints);
                W.Sound.Play(Sfx.EnemyHit);
                if (_kills >= KillsForMothership && !_motherActive)
                    LaunchMothership();
                continue;
            }
            if (Sprite.Overlaps(sprite, (int)a.X, (int)a.Y, ShipSprite, ShipX, (int)_shipY))
            {
                Die();
                return;
            }
            if (a.Life <= 0 || a.X < -24)
                _aliens.RemoveAt(i);
        }
    }

    private Sprite AlienSprite(Alien a) => Sprite.Of(AlienFrames[(W.Tick / 6 + a.Phase) % 3]);

    private void UpdateCube()
    {
        if (!_cubeActive)
        {
            if (W.Chance(0.002 + 0.0005 * W.Hardness))
            {
                _cubeActive = true;
                _cubeX = 120 + W.Rng.Next(90);
                _cubeY = 10 + W.Rng.Next(150);
            }
            return;
        }
        _cubeX -= ZorgonWorld.PerTick(7f);
        var cube = Sprite.Of(CubeFrames[W.Tick / 5 % CubeFrames.Length]);
        if (BeamHits(cube, _cubeX, _cubeY, out var beam))
            _beams.Remove(beam);   // the cube blocks beams
        if (Sprite.Overlaps(cube, (int)_cubeX, (int)_cubeY, ShipSprite, ShipX, (int)_shipY))
            Die();
        if (_cubeX < -24)
            _cubeActive = false;
    }

    private void LaunchMothership()
    {
        _motherActive = true;
        _motherX = 216;
        _motherY = 100;
        _motherLife = (int)(4f * XenZargGame.TicksPerSecond / ZorgonWorld.Speed * 1.6f);
        W.Sound.Play(Sfx.Warning);
    }

    private void UpdateMothership()
    {
        if (!_motherActive)
            return;
        _motherX -= 2 * UpdateRate;
        if (W.Chance(0.03))
            _motherDir = -_motherDir;
        _motherY = Math.Clamp(_motherY + _motherDir * UpdateRate * 2, 1, 170);
        var sprite = Sprite.Of(MotherFrames[W.Tick / 6 % 3]);
        if (BeamHits(sprite, _motherX, _motherY, out _))
        {
            _motherActive = false;
            _won = 1;
            W.Sound.Play(Sfx.Victory);
            return;
        }
        if (Sprite.Overlaps(sprite, (int)_motherX, (int)_motherY, ShipSprite, ShipX, (int)_shipY))
        {
            Die();
            return;
        }
        if (--_motherLife <= 0 || _motherX < -30)
        {
            // Escaped: another ten kills are needed.
            _motherActive = false;
            _kills = 0;
        }
    }

    private void UpdateMissiles()
    {
        var shot = Sprite.Of(SpriteData.ZAlienShotF0);
        for (int i = _missiles.Count - 1; i >= 0; i--)
        {
            var m = _missiles[i];
            m.X -= ZorgonWorld.PerTick(53f);
            if (Sprite.Overlaps(shot, (int)m.X, (int)m.Y, ShipSprite, ShipX, (int)_shipY))
            {
                Die();
                return;
            }
            if (m.X < -18)
                _missiles.RemoveAt(i);
        }
    }

    private void Die()
    {
        if (_dying > 0 || Invulnerable)
            return;
        _dying = 1;
        W.Sound.Play(Sfx.PlayerDeath);
    }

    public override void Draw(Canvas c)
    {
        for (int x = 0; x < Canvas.Width; x += 60)
            c.Draw(SpriteData.ZShipGroundStrip, x, GroundY);

        if (_materialise > 0)
        {
            // Coloured streaks as the ship beams in.
            for (int i = 0; i < 16; i++)
            {
                int y = (int)_shipY + W.Rng.Next(16);
                int len = W.Rng.Next(6, 24);
                c.Fill(6, y, len, 1, (OricColor)(1 + W.Rng.Next(7)));
            }
            if (_materialise < MaterialiseTicks / 3)
                c.Draw(ShipSprite, ShipX, (int)_shipY);
        }
        else if (_dying > 0)
        {
            // The ship dissolves.
            var s = ShipSprite;
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    if (s.ColorAt(x, y) is OricColor col && W.Rng.Next(60) > _dying)
                        c.Plot(ShipX + x, (int)_shipY + y, col);
        }
        else
        {
            c.Draw(ShipSprite, ShipX, (int)_shipY);
        }

        foreach (var b in _beams)
            c.Fill((int)b.Tail, b.Y, (int)(b.Head - b.Tail), 1, b.Color);
        foreach (var a in _aliens)
            c.Draw(AlienSprite(a), (int)a.X, (int)a.Y);
        var shot = Sprite.Of(SpriteData.ZAlienShotF0);
        foreach (var m in _missiles)
            c.Draw(shot, (int)m.X, (int)m.Y);
        if (_cubeActive)
            c.Draw(CubeFrames[W.Tick / 5 % CubeFrames.Length], (int)_cubeX, (int)_cubeY);
        if (_motherActive)
            c.Draw(MotherFrames[W.Tick / 6 % 3], (int)_motherX, (int)_motherY);
        if (_won > 0 && _won / 3 % 2 == 0)
            c.Draw(SpriteData.ZBridgePartBlock, ShipX + 24, (int)_shipY + 5);
    }
}
