using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Xenon;

/// <summary>
/// LEVEL 5: the Zorgon Battle Star. Its moving nuclear reactor is shielded by a force
/// field that the laser must burn through and by a guard formation. Hit the reactor
/// to vaporise the star; the faster you do it, the higher the bonus.
/// </summary>
public sealed class BattleStarLevel : XenonLevel
{
    public const int HullX = 72, HullY = 12;
    public const int ScaleX = 4, ScaleY = 3;
    public const int ReactorWidth = 12, ReactorHeight = 6;
    public const int FieldY = 64;
    public const int FieldCell = 4;
    public const int GuardPoints = 50;
    public const int MaxBonus = 10000;
    public const int MinBonus = 1000;
    public const int ExplosionTicks = 150;

    public sealed class Guard
    {
        public int Column, Row;
        public bool Alive = true;
    }

    private static Sprite _hull;
    private readonly PlayerShip _ship = new();
    private readonly bool[] _field;
    private readonly List<Guard> _guards = new();
    private float _reactorX;
    private float _reactorDir = 1;
    private float _formationX = 24;
    private float _formationDir = 1;
    private int _elapsed;
    private int _exploding;
    private int _bonus;

    public BattleStarLevel(XenonWorld world) : base(world)
    {
        _field = new bool[Hull.Width / FieldCell];
        Array.Fill(_field, true);
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 7; col++)
                _guards.Add(new Guard { Column = col, Row = row });
        _reactorX = HullX + 8;
    }

    public override int Number => 5;

    /// <summary>The battle star is a giant Zorgon saucer.</summary>
    public static Sprite Hull => _hull ??= Sprite.Of(SpriteData.Saucer1).Scale(ScaleX, ScaleY);

    public int ReactorY => HullY + Hull.Height + 2;
    public float ReactorX => _reactorX;
    public bool Destroyed => _exploding > 0;
    public IReadOnlyList<Guard> Guards => _guards;
    public bool[] Field => _field;
    public PlayerShip Ship => _ship;
    public override float ShipX { get => _ship.X; set => _ship.X = value; }
    public override int CompletionBonus => _bonus;
    public override string CompletionMessage => "BATTLE STAR VAPOURISED  BONUS " + _bonus;

    /// <summary>Bonus falls by 100 for every second taken, from 10000 down to 1000.</summary>
    public static int BonusFor(int ticks) =>
        Math.Max(MinBonus, MaxBonus - ticks / XenZargGame.TicksPerSecond * 100);

    private (int x, int y) GuardPosition(Guard g) =>
        ((int)_formationX + g.Column * 18, FieldY + 10 + g.Row * 16);

    public override void Update()
    {
        if (_exploding > 0)
        {
            if (_exploding % 6 == 0)
            {
                W.AddBlast(HullX + W.Rng.Next(Hull.Width), HullY + W.Rng.Next(Hull.Height));
                W.Sound.Play(Sfx.EnemyHit, (float)W.Rng.NextDouble() - 0.5f);
            }
            if (++_exploding > ExplosionTicks)
                Completed = true;
            return;
        }

        int d = W.Difficulty;
        _elapsed++;
        _ship.Update(W, true);
        W.Laser.Update();

        // Reactor shuttles along beneath the hull.
        _reactorX += _reactorDir * (0.8f + 0.1f * d);
        float minX = HullX + 4, maxX = HullX + Hull.Width - 4 - ReactorWidth;
        if (_reactorX < minX || _reactorX > maxX)
        {
            _reactorDir = -_reactorDir;
            _reactorX = Math.Clamp(_reactorX, minX, maxX);
        }

        // Guard formation sweeps side to side.
        _formationX += _formationDir * (0.4f + 0.05f * d);
        if (_formationX < 4 || _formationX > Canvas.Width - 4 - 6 * 18 - 11)
            _formationDir = -_formationDir;

        // Force field slowly regenerates.
        if (W.Tick % Math.Max(30, 120 - d * 8) == 0)
        {
            int cell = W.Rng.Next(_field.Length);
            _field[cell] = true;
        }

        if (W.Laser.Active)
            CheckLaser();

        foreach (var g in _guards)
        {
            if (!g.Alive)
                continue;
            var (gx, gy) = GuardPosition(g);
            if (W.Chance(0.0012 + 0.0004 * d))
                W.DropBomb(gx + 5, gy + 12, Sprite.Of(SpriteData.BombMagentaGreen), 2f + 0.1f * d);
        }
        if (W.Chance(0.006 + 0.001 * d))
            W.DropBomb(_reactorX + ReactorWidth / 2f, ReactorY + ReactorHeight, Sprite.Of(SpriteData.BombCyanBlue), 2.6f + 0.1f * d);

        if (W.UpdateBombs(PlayerShip.Sprite, (int)_ship.X, _ship.Y))
            Kill(_ship.CentreX, _ship.Y + 9);
    }

    private void CheckLaser()
    {
        var laser = W.Laser;
        int lx = (int)laser.X;
        int ly = (int)laser.Y;

        // Guards first (they are lowest).
        var guardSprite = Sprite.Of(SpriteData.SmallAard1);
        foreach (var g in _guards)
        {
            if (!g.Alive)
                continue;
            var (gx, gy) = GuardPosition(g);
            if (laser.Hits(guardSprite, gx, gy))
            {
                g.Alive = false;
                laser.Active = false;
                W.Session.AddScore(GuardPoints);
                W.AddBlast(gx + 5, gy + 6);
                W.Sound.Play(Sfx.EnemyHit);
                return;
            }
        }

        // Force field: one cell burns away per hit.
        if (ly <= FieldY + 2 && ly + Laser.Height >= FieldY)
        {
            int cell = (lx - HullX) / FieldCell;
            if (cell >= 0 && cell < _field.Length && _field[cell])
            {
                _field[cell] = false;
                laser.Active = false;
                W.Sound.Play(Sfx.Zap);
                return;
            }
        }

        // Reactor.
        if (lx + Laser.Width > _reactorX && lx < _reactorX + ReactorWidth &&
            ly <= ReactorY + ReactorHeight && ly + Laser.Height >= ReactorY)
        {
            laser.Active = false;
            _bonus = BonusFor(_elapsed);
            _exploding = 1;
            W.Bombs.Clear();
            W.Sound.Play(Sfx.PlayerDeath);
            return;
        }

        // The hull absorbs everything else.
        if (Sprite.OverlapsRect(Hull, HullX, HullY, lx, ly, Laser.Width, Laser.Height))
            laser.Active = false;
    }

    public override void Respawn()
    {
        base.Respawn();
        _ship.X = (Canvas.Width - PlayerShip.Sprite.Width) / 2f;
    }

    public override void Draw(Canvas c)
    {
        bool flash = _exploding > 0 && _exploding / 3 % 2 == 0;
        if (flash)
            c.DrawTinted(Hull, HullX, HullY, (OricColor)(1 + _exploding / 3 % 7));
        else
            c.Draw(Hull, HullX, HullY);

        if (_exploding == 0)
        {
            // Nuclear reactor: pulsing core in its housing.
            int rx = (int)_reactorX;
            c.Fill(rx, ReactorY, ReactorWidth, ReactorHeight, OricColor.Red);
            var core = W.Tick / 4 % 2 == 0 ? OricColor.Yellow : OricColor.White;
            c.Fill(rx + 2, ReactorY + 1, ReactorWidth - 4, ReactorHeight - 2, core);

            for (int i = 0; i < _field.Length; i++)
                if (_field[i])
                    c.Fill(HullX + i * FieldCell, FieldY, FieldCell, 2, (W.Tick / 2 + i) % 5 == 0 ? OricColor.White : OricColor.Cyan);

            var guardSprite = Sprite.Of(W.Tick / 10 % 2 == 0 ? SpriteData.SmallAard1 : SpriteData.SmallAard2);
            foreach (var g in _guards)
            {
                if (!g.Alive)
                    continue;
                var (gx, gy) = GuardPosition(g);
                c.Draw(guardSprite, gx, gy);
            }
        }

        if (!PlayerHit)
            _ship.Draw(c);
    }
}
