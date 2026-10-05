using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Xenon;

/// <summary>
/// LEVEL 2: large Aards drift about. When hit an Aard clones into two small Aards,
/// one of which dives at the player and explodes on impact with the ground; the
/// radiation from the explosion is fatal, so keep away.
/// </summary>
public sealed class AardLevel : XenonLevel
{
    public const int LargePoints = 100;
    public const int SmallPoints = 150;
    public const int LargeCount = 4;
    public const int RadiationRadius = 22;
    public const int RadiationTicks = 60;
    public const int ImpactY = 172;

    public enum Kind { Large, Small, Diver }

    public sealed class Aard
    {
        public Kind Kind;
        public float X, Y, Vx, Vy;
        public int Phase;
    }

    public sealed class Radiation
    {
        public int X, Y, Timer;
    }

    private readonly List<Aard> _aards = new();
    private readonly List<Radiation> _radiation = new();
    private readonly PlayerShip _ship = new();

    public AardLevel(XenonWorld world) : base(world)
    {
        for (int i = 0; i < LargeCount; i++)
        {
            _aards.Add(new Aard
            {
                Kind = Kind.Large,
                X = 30 + i * 50 + W.Rng.Next(-10, 10),
                Y = 24 + W.Rng.Next(0, 70),
                Vx = RandomSpeed(),
                Vy = RandomSpeed() * 0.7f,
                Phase = i,
            });
        }
    }

    public override int Number => 2;
    public IReadOnlyList<Aard> Aards => _aards;
    public IReadOnlyList<Radiation> RadiationZones => _radiation;
    public PlayerShip Ship => _ship;
    public override float ShipX { get => _ship.X; set => _ship.X = value; }

    private float RandomSpeed()
    {
        float s = 0.5f + 0.06f * W.Difficulty + (float)W.Rng.NextDouble() * 0.5f;
        return W.Rng.Next(2) == 0 ? -s : s;
    }

    private static Sprite SpriteFor(Aard a, int tick)
    {
        bool alt = (tick / 10 + a.Phase) % 2 == 0;
        return a.Kind == Kind.Large
            ? Sprite.Of(alt ? SpriteData.LargeAard1 : SpriteData.LargeAard2)
            : Sprite.Of(alt ? SpriteData.SmallAard1 : SpriteData.SmallAard2);
    }

    public override void Update()
    {
        int d = W.Difficulty;
        _ship.Update(W, true);
        W.Laser.Update();

        for (int i = _aards.Count - 1; i >= 0; i--)
        {
            var a = _aards[i];
            var sprite = SpriteFor(a, W.Tick);
            if (a.Kind == Kind.Diver)
            {
                float target = _ship.CentreX - sprite.Width / 2f;
                a.X += Math.Clamp((target - a.X) * 0.04f, -1.5f, 1.5f);
                a.Y += a.Vy;
                if (a.Y + sprite.Height >= ImpactY)
                {
                    _aards.RemoveAt(i);
                    Explode((int)a.X + sprite.Width / 2, ImpactY);
                    continue;
                }
            }
            else
            {
                a.X += a.Vx;
                a.Y += a.Vy;
                if (a.X < 0 || a.X > Canvas.Width - sprite.Width)
                {
                    a.Vx = -a.Vx;
                    a.X = Math.Clamp(a.X, 0, Canvas.Width - sprite.Width);
                }
                if (a.Y < 14 || a.Y > 130)
                {
                    a.Vy = -a.Vy;
                    a.Y = Math.Clamp(a.Y, 14, 130);
                }
                if (W.Chance(0.0025 + 0.0007 * d))
                    W.DropBomb(a.X + sprite.Width / 2f, a.Y + sprite.Height, Sprite.Of(SpriteData.BombCyanGreen), 2.2f + 0.1f * d);
            }

            if (W.Laser.Hits(sprite, a.X, a.Y))
            {
                W.Laser.Active = false;
                _aards.RemoveAt(i);
                W.AddBlast((int)a.X + sprite.Width / 2, (int)a.Y + sprite.Height / 2);
                W.Sound.Play(Sfx.EnemyHit);
                if (a.Kind == Kind.Large)
                {
                    W.Session.AddScore(LargePoints);
                    Clone(a);
                }
                else
                {
                    W.Session.AddScore(SmallPoints);
                }
                continue;
            }

            if (Sprite.Overlaps(sprite, (int)a.X, (int)a.Y, PlayerShip.Sprite, (int)_ship.X, _ship.Y))
            {
                _aards.RemoveAt(i);
                Kill(_ship.CentreX, _ship.Y + 9);
            }
        }

        for (int i = _radiation.Count - 1; i >= 0; i--)
        {
            var r = _radiation[i];
            int dx = _ship.CentreX - r.X;
            int dy = _ship.Y + 9 - r.Y;
            if (dx * dx + dy * dy <= RadiationRadius * RadiationRadius)
                Kill(_ship.CentreX, _ship.Y + 9);
            if (--r.Timer <= 0)
                _radiation.RemoveAt(i);
        }

        if (W.UpdateBombs(PlayerShip.Sprite, (int)_ship.X, _ship.Y))
            Kill(_ship.CentreX, _ship.Y + 9);

        if (_aards.Count == 0 && _radiation.Count == 0)
            Completed = true;
    }

    private void Clone(Aard parent)
    {
        W.Sound.Play(Sfx.Clone);
        _aards.Add(new Aard
        {
            Kind = Kind.Small,
            X = parent.X,
            Y = parent.Y,
            Vx = RandomSpeed() * 1.2f,
            Vy = RandomSpeed() * 0.8f,
            Phase = parent.Phase,
        });
        _aards.Add(new Aard
        {
            Kind = Kind.Diver,
            X = parent.X + 6,
            Y = parent.Y + 6,
            Vy = 1.3f + 0.1f * W.Difficulty,
            Phase = parent.Phase + 1,
        });
        W.Sound.Play(Sfx.Dive);
    }

    private void Explode(int x, int y)
    {
        _radiation.Add(new Radiation { X = x, Y = y, Timer = RadiationTicks });
        W.Sound.Play(Sfx.PlayerDeath);
    }

    public override void Respawn()
    {
        base.Respawn();
        _ship.X = (Canvas.Width - PlayerShip.Sprite.Width) / 2f;
        _radiation.Clear();
        _aards.RemoveAll(a => a.Kind == Kind.Diver);
    }

    public override void Draw(Canvas c)
    {
        foreach (var a in _aards)
            c.Draw(SpriteFor(a, W.Tick), (int)a.X, (int)a.Y);

        var star = Sprite.Of(SpriteData.Explosion);
        foreach (var r in _radiation)
        {
            // Pulsing radiation cloud of starbursts in the flashing Oric colours.
            var ink = (OricColor)(1 + (r.Timer / 3) % 7);
            int spread = RadiationRadius - 8;
            for (int k = 0; k < 6; k++)
            {
                double ang = k * Math.PI / 3 + r.Timer * 0.15;
                int px = r.X + (int)(Math.Cos(ang) * spread) - 5;
                int py = r.Y + (int)(Math.Sin(ang) * spread * 0.6) - 6;
                c.DrawTinted(star, px, py, ink);
            }
            c.DrawTinted(star, r.X - 5, r.Y - 6, OricColor.White);
        }

        if (!PlayerHit)
            _ship.Draw(c);
    }
}
