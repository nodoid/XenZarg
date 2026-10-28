using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Genesis.Missions;

/// <summary>A chain of worm segments led by a head, swimming freely across Xer.</summary>
public sealed class Worm
{
    public int Length;
    public float HeadX;
    public int Y;
    public int Dir;
    public float Speed;
    public OricColor Colour;

    public const int SegmentStep = 7;

    public float SegmentX(int index) => HeadX - Dir * index * SegmentStep;

    public Sprite HeadSprite => Sprite.Of(SpriteData.GWormHead).Recolor(Colour);
    public Sprite BodySprite => Sprite.Of(SpriteData.GWormBody).Recolor(Colour);

    public Sprite SegmentSprite(int index) => index == 0 ? HeadSprite : BodySprite;
}

/// <summary>
/// MISSION XER: steer the boomerang through the wormy creatures over Xer's scan-line
/// landscape. Every segment sliced counts towards the catch; a body segment touching the
/// probe costs a shield.
/// </summary>
public sealed class WormMission : GenesisMission
{
    public const int HeadsNeeded = 25;

    private int _spawnTimer = 100;

    public WormMission(GenesisWorld world) : base(world, Planet.Xer)
    {
        for (int i = 0; i < 5; i++)
            SpawnWorm();
    }

    public GenesisShip Ship { get; } = new();
    public Boomerang Boomerang { get; } = new();
    public List<Worm> Worms { get; } = new();
    public int Heads { get; private set; }

    public override int Specimens => Heads;
    public override int Goal => HeadsNeeded;
    public override int CompletionBonus => 2500;

    public override void Update()
    {
        if (Completed || Failed)
            return;

        Ship.Update(W);
        if (W.Controls.FirePressed)
            Boomerang.TryThrow(Ship);
        Boomerang.Update(W, Ship);

        foreach (var worm in Worms)
        {
            worm.HeadX += worm.Dir * worm.Speed;
            if (worm.Dir > 0 && worm.HeadX - (worm.Length - 1) * Worm.SegmentStep > Canvas.Width)
                worm.HeadX = -Worm.SegmentStep;
            else if (worm.Dir < 0 && worm.HeadX + (worm.Length - 1) * Worm.SegmentStep < 0)
                worm.HeadX = Canvas.Width + Worm.SegmentStep;
        }

        if (Boomerang.Active)
        {
            for (int w = 0; w < Worms.Count; w++)
            {
                var worm = Worms[w];
                int cut = -1;
                for (int i = 0; i < worm.Length; i++)
                {
                    var sprite = worm.SegmentSprite(i);
                    if (!Boomerang.Hits(sprite, (int)worm.SegmentX(i), worm.Y))
                        continue;
                    cut = i;
                    break;
                }
                if (cut < 0)
                    continue;
                Cut(worm, cut);
                break;
            }
        }

        UpdateShipCollisions();

        if (--_spawnTimer <= 0)
        {
            _spawnTimer = Math.Max(50, 120 - W.Hardness * 6);
            int total = 0;
            foreach (var w in Worms)
                total += w.Length;
            if (total < 24)
                SpawnWorm();
        }

        if (Heads >= HeadsNeeded)
            Completed = true;
    }

    private void SpawnWorm()
    {
        int dir = W.Rng.Next(2) == 0 ? 1 : -1;
        int length = 5 + W.Rng.Next(6);
        OricColor[] colours = [OricColor.Magenta, OricColor.Cyan, OricColor.Yellow, OricColor.Green];
        Worms.Add(new Worm
        {
            Y = 28 + W.Rng.Next(112),
            Dir = dir,
            Length = length,
            HeadX = dir > 0 ? -Worm.SegmentStep : Canvas.Width + Worm.SegmentStep,
            Speed = 0.35f + W.Hardness * 0.025f + (float)W.Rng.NextDouble() * 0.25f,
            Colour = colours[W.Rng.Next(colours.Length)],
        });
    }

    private void Cut(Worm worm, int index)
    {
        W.Sound.Play(Sfx.EnemyHit);
        Heads++;
        W.AddScore(20);

        int rear = worm.Length - index - 1;
        float rearHead = worm.SegmentX(index + 1);
        worm.Length = index;
        if (worm.Length <= 0)
            Worms.Remove(worm);
        if (rear > 0)
        {
            Worms.Add(new Worm
            {
                Y = worm.Y,
                Dir = worm.Dir,
                HeadX = rearHead,
                Length = rear,
                Speed = worm.Speed,
                Colour = worm.Colour,
            });
        }
    }

    private void UpdateShipCollisions()
    {
        var ship = GenesisShip.Sprite;
        for (int i = Worms.Count - 1; i >= 0; i--)
        {
            var worm = Worms[i];
            if (worm.Length < 2)
                continue;
            for (int s = 1; s < worm.Length; s++)
            {
                if (!Sprite.Overlaps(worm.BodySprite, (int)worm.SegmentX(s), worm.Y,
                        ship, (int)Ship.X, GenesisShip.Top))
                    continue;
                Worms.RemoveAt(i);
                Boomerang.Cancel();
                Damage(Ship);
                break;
            }
        }
    }

    public override void Draw(Canvas c)
    {
        // Xer is a planet like the others: its scan-line landscape runs right across the
        // screen and the worms swim over it.
        GenesisArt.DrawWaves(c, GenesisPlanets.Colour(Planet), OricColor.White, W.Tick);

        foreach (var worm in Worms)
            for (int i = worm.Length - 1; i >= 0; i--)
                c.Draw(worm.SegmentSprite(i), (int)worm.SegmentX(i), worm.Y);

        Boomerang.Draw(c);
        W.DrawProbe(c, Ship);
    }
}
