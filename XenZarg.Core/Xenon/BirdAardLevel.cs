using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;

namespace XenZarg.Core.Xenon;

/// <summary>
/// LEVEL 1: four Bird-Aards wheel in a hypnotic rotating formation, bombing and
/// peeling off to dive at the player. 150 points each.
/// </summary>
public sealed class BirdAardLevel : XenonLevel
{
    public const int Points = 150;
    public const int BirdCount = 4;

    public sealed class Bird
    {
        public int Slot;
        public float X, Y;
        public bool Alive = true;
        public bool Diving;
        public bool Returning;
        public float Vx, Vy;
        public int ReturnTimer;
    }

    private readonly List<Bird> _birds = new();
    private readonly PlayerShip _ship = new();
    private float _angle;
    private readonly Sprite _up = Sprite.Of(SpriteData.BirdAardUp);
    private readonly Sprite _down = Sprite.Of(SpriteData.BirdAardDown);

    public BirdAardLevel(XenonWorld world) : base(world)
    {
        for (int i = 0; i < BirdCount; i++)
            _birds.Add(new Bird { Slot = i });
        PlaceInFormation(true);
    }

    public override int Number => 1;
    public IReadOnlyList<Bird> Birds => _birds;
    public PlayerShip Ship => _ship;

    public override float ShipX { get => _ship.X; set => _ship.X = value; }

    private (float x, float y) SlotPosition(int slot)
    {
        int t = W.Tick;
        float cx = 120 + 70 * MathF.Sin(t * 0.011f);
        float cy = 72 + 18 * MathF.Sin(t * 0.017f);
        float r = 26 + 8 * MathF.Sin(t * 0.023f);
        float a = _angle + slot * MathF.PI / 2;
        return (cx + r * MathF.Cos(a) - 9, cy + r * 0.8f * MathF.Sin(a) - 9);
    }

    private void PlaceInFormation(bool all)
    {
        foreach (var b in _birds)
        {
            if (!all && (b.Diving || b.Returning))
                continue;
            (b.X, b.Y) = SlotPosition(b.Slot);
        }
    }

    public override void Update()
    {
        int d = W.Difficulty;
        _angle += 0.03f + 0.002f * d;
        PlaceInFormation(false);

        _ship.Update(W, true);
        W.Laser.Update();

        int diving = 0;
        foreach (var b in _birds)
            if (b.Alive && b.Diving)
                diving++;

        foreach (var b in _birds)
        {
            if (!b.Alive)
                continue;
            if (b.Diving)
            {
                float target = _ship.CentreX - 9;
                b.Vx = Math.Clamp((target - b.X) * 0.03f, -1.4f, 1.4f);
                b.X += b.Vx;
                b.Y += b.Vy;
                if (b.Y > XenonWorld.PlayBottom)
                {
                    b.Diving = false;
                    b.Returning = true;
                    b.ReturnTimer = 60;
                    b.Y = -18;
                }
            }
            else if (b.Returning)
            {
                var (sx, sy) = SlotPosition(b.Slot);
                float k = 1f / Math.Max(1, b.ReturnTimer);
                b.X += (sx - b.X) * k;
                b.Y += (sy - b.Y) * k;
                if (--b.ReturnTimer <= 0)
                    b.Returning = false;
            }
            else if (diving < 1 + d / 5 && W.Chance(0.0015 + 0.0005 * d))
            {
                b.Diving = true;
                b.Vy = 1.6f + 0.12f * d;
                diving++;
                W.Sound.Play(Sfx.Dive);
            }

            if (b.Y > 0 && b.Y < 150 && W.Chance(0.003 + 0.0008 * d))
                W.DropBomb(b.X + 9, b.Y + 18, Sprite.Of(SpriteData.BombWhiteRed), 2.2f + 0.1f * d);

            var sprite = SpriteFor(b);
            if (W.Laser.Hits(sprite, b.X, b.Y))
            {
                b.Alive = false;
                W.Laser.Active = false;
                W.Session.AddScore(Points);
                W.AddBlast((int)b.X + 9, (int)b.Y + 9);
                W.Sound.Play(Sfx.EnemyHit);
            }
            else if (Sprite.Overlaps(sprite, (int)b.X, (int)b.Y, PlayerShip.Sprite, (int)_ship.X, _ship.Y))
            {
                b.Alive = false;
                Kill(_ship.CentreX, _ship.Y + 9);
            }
        }

        if (W.UpdateBombs(PlayerShip.Sprite, (int)_ship.X, _ship.Y))
            Kill(_ship.CentreX, _ship.Y + 9);

        if (_birds.TrueForAll(b => !b.Alive))
            Completed = true;
    }

    private Sprite SpriteFor(Bird b) => (W.Tick / 8 + b.Slot) % 2 == 0 ? _up : _down;

    public override void Respawn()
    {
        base.Respawn();
        _ship.X = (Canvas.Width - PlayerShip.Sprite.Width) / 2f;
        foreach (var b in _birds)
        {
            b.Diving = false;
            b.Returning = false;
        }
        PlaceInFormation(true);
    }

    public override void Draw(Canvas c)
    {
        foreach (var b in _birds)
            if (b.Alive)
                c.Draw(SpriteFor(b), (int)b.X, (int)b.Y);
        if (!PlayerHit)
            _ship.Draw(c);
    }
}
