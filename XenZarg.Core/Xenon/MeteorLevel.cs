using System;
using System.Collections.Generic;
using XenZarg.Core.Graphics;

namespace XenZarg.Core.Xenon;

/// <summary>
/// LEVEL 3: travel through deep space avoiding the meteorite shower.
/// The laser is useless here; each meteorite that passes scores 10.
/// </summary>
public sealed class MeteorLevel : XenonLevel
{
    public const int PointsPerMeteor = 10;
    public const int MeteorTotal = 30;

    public sealed class Meteor
    {
        public float X, Y, Speed;
        public int Phase;
    }

    private static readonly string[][] Frames = [SpriteData.Meteor1, SpriteData.Meteor2, SpriteData.Meteor3];
    private readonly List<Meteor> _meteors = new();
    private readonly PlayerShip _ship = new();
    private int _spawned;
    private int _passed;
    private int _spawnTimer = 30;

    public MeteorLevel(XenonWorld world) : base(world)
    {
    }

    public override int Number => 3;
    public int Passed => _passed;
    public IReadOnlyList<Meteor> Meteors => _meteors;
    public PlayerShip Ship => _ship;
    public override float ShipX { get => _ship.X; set => _ship.X = value; }

    public static Sprite FrameFor(Meteor m, int tick) => Sprite.Of(Frames[(tick / 4 + m.Phase) % Frames.Length]);

    public override void Update()
    {
        int d = W.Difficulty;
        _ship.Update(W, false);

        int maxActive = Math.Min(8, 3 + d / 2);
        if (--_spawnTimer <= 0 && _spawned < MeteorTotal && _meteors.Count < maxActive)
        {
            _meteors.Add(new Meteor
            {
                X = W.Rng.Next(0, Canvas.Width - 10),
                Y = XenonWorld.PlayTop - 11,
                Speed = 1.2f + (float)W.Rng.NextDouble() * 1.5f + 0.1f * d,
                Phase = W.Rng.Next(3),
            });
            _spawned++;
            _spawnTimer = W.Rng.Next(Math.Max(8, 30 - d * 2), Math.Max(16, 60 - d * 3));
        }

        for (int i = _meteors.Count - 1; i >= 0; i--)
        {
            var m = _meteors[i];
            m.Y += m.Speed;
            var sprite = FrameFor(m, W.Tick);
            if (Sprite.Overlaps(sprite, (int)m.X, (int)m.Y, PlayerShip.Sprite, (int)_ship.X, _ship.Y))
            {
                _meteors.RemoveAt(i);
                Kill(_ship.CentreX, _ship.Y + 9);
                continue;
            }
            if (m.Y >= XenonWorld.PlayBottom)
            {
                _meteors.RemoveAt(i);
                _passed++;
                W.Session.AddScore(PointsPerMeteor);
            }
        }

        if (_spawned >= MeteorTotal && _meteors.Count == 0)
            Completed = true;
    }

    public override void Respawn()
    {
        base.Respawn();
        _ship.X = (Canvas.Width - PlayerShip.Sprite.Width) / 2f;
        _meteors.Clear();
        _spawnTimer = 50;
    }

    public override void Draw(Canvas c)
    {
        foreach (var m in _meteors)
            c.Draw(FrameFor(m, W.Tick), (int)m.X, (int)m.Y);
        if (!PlayerHit)
            _ship.Draw(c);
    }
}
