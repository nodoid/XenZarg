using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Genesis.Missions;

/// <summary>A Zorgon in the final storm, running and diving towards the Genesis Probe.</summary>
public sealed class Zorgon
{
    public float X;
    public float Y;
    public int Age;
    public float Speed;
    public int Wave;
}

/// <summary>
/// MISSION RADON: "Zorgons storm ahead". Five waves of Zorgons sweep down towards
/// the probe. There is no weapon here: steer the probe through the storm and survive.
/// </summary>
public sealed class RadonBattle : GenesisMission
{
    public const int Waves = 5;

    private int _spawnTimer = 30;
    private int _left;
    private int _pause;

    public RadonBattle(GenesisWorld world) : base(world, Planet.Radon)
    {
        Wave = 1;
        _left = EnemiesFor(1);
    }

    public GenesisShip Ship { get; } = new();
    public List<Zorgon> Enemies { get; } = new();
    public int Wave { get; private set; }

    public override int Specimens => Math.Min(Goal, Wave - 1);
    public override int Goal => Waves;
    public override int CompletionBonus => 5000;

    private int EnemiesFor(int wave) => 7 + wave * 3;

    public override void Update()
    {
        if (Completed || Failed)
            return;

        Ship.Update(W);

        if (_pause > 0)
        {
            if (--_pause == 0)
            {
                if (Wave >= Waves)
                {
                    Completed = true;
                    return;
                }
                Wave++;
                _left = EnemiesFor(Wave);
                _spawnTimer = 20;
                W.Sound.Play(Sfx.Warning);
            }
            return;
        }

        if (_left > 0 && --_spawnTimer <= 0)
        {
            _spawnTimer = Math.Max(10, 34 - Wave * 3 - W.Hardness);
            _left--;
            Enemies.Add(new Zorgon
            {
                X = 10 + W.Rng.Next(Canvas.Width - 20),
                Y = -10,
                Speed = 0.8f + W.Hardness * 0.04f + Wave * 0.12f,
                Wave = Wave,
            });
        }

        var ship = GenesisShip.Sprite;
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var z = Enemies[i];
            z.Age++;
            z.Y += z.Speed;
            z.X = Math.Clamp(z.X + (float)Math.Sin(z.Age * 0.08 + z.Wave) * 0.8f, 0, Canvas.Width - 8);

            var sprite = Frame(z);
            if (Sprite.Overlaps(sprite, (int)z.X, (int)z.Y, ship, (int)Ship.X, GenesisShip.Top))
            {
                Enemies.RemoveAt(i);
                Damage(Ship);
                if (Failed)
                    return;
                continue;
            }
            if (z.Y > GenesisShip.Top + 20)
            {
                Enemies.RemoveAt(i);
                W.AddScore(25);
            }
        }

        if (_left == 0 && Enemies.Count == 0)
        {
            _pause = Wave >= Waves ? 60 : 45;
            if (Wave >= Waves)
                W.Sound.Play(Sfx.Victory);
        }
    }

    private static Sprite Frame(Zorgon z) =>
        Sprite.Of((z.Age / 5) % 2 == 0 ? SpriteData.ZAntF0 : SpriteData.ZAntF1);

    public override void Draw(Canvas c)
    {
        c.Fill(0, 0, Canvas.Width, Canvas.Height, OricColor.Black);
        for (int i = 0; i < 40; i++)
        {
            int x = (i * 53 + 17) % Canvas.Width;
            int y = (i * 37 + 11) % 190;
            c.Plot(x, y, i % 4 == 0 ? OricColor.White : OricColor.Magenta);
        }

        foreach (var z in Enemies)
            c.Draw(Frame(z), (int)z.X, (int)z.Y);

        W.DrawProbe(c, Ship);
        c.TextAt(2, 4, "WAVE " + Wave + "/" + Waves, OricColor.Yellow);
        c.TextAt(12, 4, "SHIELDS", OricColor.Cyan);
        c.TextAt(20, 4, W.Session.Bases.ToString(), OricColor.White);
    }
}
