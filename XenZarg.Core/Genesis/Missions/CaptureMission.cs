using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Genesis.Missions;

/// <summary>One animal crossing a scan-line lane of Rundus or Terra.</summary>
public sealed class Creature
{
    public int Species;
    public float X;
    public int Lane;
    public int Dir;
    public float Speed;
    public bool Alive = true;

    public int Y => GenesisArt.LaneRows[Lane] - Sprite.Height + 2;

    public Sprite Sprite => Graphics.Sprite.Of(GenesisPlanets.Species(Planet)[Species].Sprite);

    public Planet Planet { get; init; } = Planet.Rundus;
}

/// <summary>A red barrel, worth catching before it leaves the screen.</summary>
public sealed class Barrel
{
    public float X;
    public int Y;
    public int Dir = 1;
    public float Speed = 0.9f;
    public bool Alive = true;
}

public enum TransitKind
{
    Species,
    Barrel,
}

/// <summary>Something teleported aboard and flying into the hold.</summary>
public sealed class Transit
{
    public TransitKind Kind;
    public int Species;
    public Sprite Sprite;
    public float X;
    public float Y;
    public int Ticks = 24;
}

/// <summary>
/// The planet missions: aim the teleport beam with left/right and fire. Two of every
/// species must be caught, and two red barrels must not be allowed to roll off the screen.
/// This follows the original's five-platform capture screens on Rundus, Somtana, Nuciro and Terra.
/// </summary>
public sealed class CaptureMission : GenesisMission
{
    public const int TransportLimit = 2;

    private readonly SpeciesDef[] _species;
    private readonly int[] _counts;
    private readonly List<Transit> _transits = new();
    private int _spawnTimer;
    private int _barrelTimer;
    private int _missedBarrels;
    private int _beamTicks;
    private string _notice;
    private int _noticeTicks;

    public CaptureMission(GenesisWorld world, Planet planet) : base(world, planet)
    {
        _species = GenesisPlanets.Species(planet);
        _counts = new int[_species.Length];
        _barrelTimer = 240;
    }

    public GenesisShip Ship { get; } = new();
    public IReadOnlyList<SpeciesDef> Species => _species;
    public IReadOnlyList<int> Counts => _counts;
    public List<Creature> Creatures { get; } = new();
    public List<Barrel> Barrels { get; } = new();
    public List<Transit> Transits => _transits;

    public int MissedBarrels => _missedBarrels;

    /// <summary>What is still to be caught on this planet.</summary>
    public int Remaining => Math.Max(0, Goal - Specimens);

    /// <summary>The line explaining a barrel that got away, while it is on screen.</summary>
    public string Notice => _noticeTicks > 0 ? _notice : null;

    /// <summary>Ticks the teleport beam stays up after a shot.</summary>
    public const int BeamTicks = 12;

    /// <summary>The beam is only drawn while firing or while something is beaming aboard.</summary>
    public bool BeamOn => _beamTicks > 0 || _transits.Count > 0;

    public int RequiredEach => GenesisPlanets.RequiredEach(Planet);
    public override int Specimens
    {
        get
        {
            int n = 0;
            foreach (int c in _counts)
                n += c;
            return n;
        }
    }

    public override int Goal => _species.Length * RequiredEach;
    public override int CompletionBonus => Planet == Planet.Terra ? 3000 : 2000;

    public override void Update()
    {
        if (Completed || Failed)
            return;

        Ship.Update(W);
        if (_beamTicks > 0)
            _beamTicks--;
        if (_noticeTicks > 0)
            _noticeTicks--;
        UpdateTransits();
        HandleFire();
        UpdateCreatures();
        UpdateBarrels();

        for (int i = 0; i < _species.Length; i++)
            if (_counts[i] < RequiredEach)
                return;
        Completed = true;
    }

    private void HandleFire()
    {
        if (!W.Controls.FirePressed)
            return;
        _beamTicks = BeamTicks;
        if (_transits.Count >= TransportLimit)
        {
            W.Sound.Play(Sfx.Warning);
            return;
        }

        int beamX = Ship.CentreX;
        Creature bestCreature = null;
        Barrel bestBarrel = null;
        int bestY = int.MaxValue;

        foreach (var c in Creatures)
        {
            if (!c.Alive || !Covers(c.X, c.Sprite.Width, beamX))
                continue;
            if (c.Y < bestY)
            {
                bestY = c.Y;
                bestCreature = c;
                bestBarrel = null;
            }
        }
        foreach (var b in Barrels)
        {
            var sprite = Graphics.Sprite.Of(SpriteData.GBarrel);
            if (!b.Alive || !Covers(b.X, sprite.Width, beamX) || b.Y >= bestY)
                continue;
            bestY = b.Y;
            bestBarrel = b;
            bestCreature = null;
        }

        if (bestCreature != null)
        {
            bestCreature.Alive = false;
            Creatures.Remove(bestCreature);
            _transits.Add(new Transit
            {
                Kind = TransitKind.Species,
                Species = bestCreature.Species,
                Sprite = bestCreature.Sprite,
                X = bestCreature.X,
                Y = bestCreature.Y,
            });
            W.Sound.Play(Sfx.Clone);
            return;
        }
        if (bestBarrel != null)
        {
            bestBarrel.Alive = false;
            Barrels.Remove(bestBarrel);
            _transits.Add(new Transit
            {
                Kind = TransitKind.Barrel,
                Species = -1,
                Sprite = Graphics.Sprite.Of(SpriteData.GBarrel),
                X = bestBarrel.X,
                Y = bestBarrel.Y,
            });
            W.Sound.Play(Sfx.Clone);
            return;
        }
        W.Sound.Play(Sfx.Shoot);
    }

    private static bool Covers(float x, int w, int beamX) => beamX >= x && beamX < x + w;

    private void UpdateTransits()
    {
        for (int i = _transits.Count - 1; i >= 0; i--)
        {
            var t = _transits[i];
            float tx = Ship.CentreX - t.Sprite.Width / 2f;
            float ty = GenesisShip.Top;
            float dx = tx - t.X;
            float dy = ty - t.Y;
            t.X += Math.Clamp(dx, -3f, 3f);
            t.Y += Math.Clamp(dy, -3f, 3f);
            if (--t.Ticks <= 0)
            {
                _transits.RemoveAt(i);
                switch (t.Kind)
                {
                    case TransitKind.Species:
                        _counts[t.Species] = Math.Min(RequiredEach, _counts[t.Species] + 1);
                        W.AddScore(50);
                        W.Sound.Play(Sfx.Pickup);
                        break;
                    default:
                        W.AddScore(150);
                        W.Sound.Play(Sfx.Pickup);
                        break;
                }
            }
        }
    }

    private void UpdateCreatures()
    {
        foreach (var c in Creatures)
        {
            c.X += c.Dir * c.Speed;
            var sprite = c.Sprite;
            if (c.Dir > 0 && c.X > Canvas.Width)
                c.X = -sprite.Width;
            else if (c.Dir < 0 && c.X + sprite.Width < 0)
                c.X = Canvas.Width;
        }

        if (--_spawnTimer > 0)
            return;
        int onScreen = 10 + Math.Min(4, W.Hardness / 2);
        if (Creatures.Count >= onScreen)
            return;
        _spawnTimer = Math.Max(18, 42 - W.Hardness * 2);
        var candidates = new List<int>();
        for (int i = 0; i < _species.Length; i++)
        {
            int inTransit = 0;
            foreach (var t in _transits)
                if (t.Kind == TransitKind.Species && t.Species == i)
                    inTransit++;
            if (_counts[i] + inTransit < RequiredEach)
                candidates.Add(i);
        }
        if (candidates.Count == 0)
            return;
        int species = candidates[W.Rng.Next(candidates.Count)];
        int dir = W.Rng.Next(2) == 0 ? 1 : -1;
        var spawn = new Creature
        {
            Species = species,
            Planet = this.Planet,
            Lane = W.Rng.Next(GenesisArt.LaneRows.Length),
            Dir = dir,
            Speed = 0.55f + W.Hardness * 0.035f + (float)W.Rng.NextDouble() * 0.35f,
            X = dir > 0 ? -Graphics.Sprite.Of(_species[species].Sprite).Width : Canvas.Width,
        };
        Creatures.Add(spawn);
    }

    private void UpdateBarrels()
    {
        if (--_barrelTimer <= 0)
        {
            _barrelTimer = Math.Max(260, 460 - W.Hardness * 15);
            int dir = W.Rng.Next(2) == 0 ? 1 : -1;
            Barrels.Add(new Barrel
            {
                Dir = dir,
                Speed = 0.8f + W.Hardness * 0.03f,
                X = dir > 0 ? -7 : Canvas.Width,
                Y = GenesisArt.LaneRows[W.Rng.Next(GenesisArt.LaneRows.Length)] - 5,
            });
        }

        var barrelSprite = Graphics.Sprite.Of(SpriteData.GBarrel);
        for (int i = Barrels.Count - 1; i >= 0; i--)
        {
            var b = Barrels[i];
            b.X += b.Dir * b.Speed;
            bool gone = b.Dir > 0 ? b.X > Canvas.Width : b.X + barrelSprite.Width < 0;
            if (!gone)
                continue;
            Barrels.RemoveAt(i);
            _missedBarrels++;
            W.Sound.Play(Sfx.Warning);
            if (_missedBarrels >= 2)
            {
                _missedBarrels = 0;
                ShowNotice("TWO BARRELS LOST - SHIELD GONE");
                Damage(Ship);
            }
            else
            {
                ShowNotice("BARREL ESCAPED - SHIELD AT RISK");
            }
        }
    }

    public override void Draw(Canvas c)
    {
        GenesisArt.DrawWaves(c, GenesisPlanets.Colour(Planet), OricColor.White, W.Tick);

        foreach (var b in Barrels)
            c.Draw(Graphics.Sprite.Of(SpriteData.GBarrel), (int)b.X, b.Y);
        foreach (var creature in Creatures)
            c.Draw(creature.Sprite, (int)creature.X, creature.Y);

        // The teleport beam is a dotted yellow line from the ship to the sky, shown
        // only while it is firing or a specimen is beaming aboard.
        if (BeamOn)
        {
            int beamX = Ship.CentreX;
            for (int y = 16; y < GenesisShip.Top; y += 5)
                c.Plot(beamX, y, OricColor.Yellow);
        }

        W.DrawProbe(c, Ship);
        foreach (var t in _transits)
            c.Draw(t.Sprite, (int)t.X, (int)t.Y);

        // How many specimens are still to be caught, and why a shield just went.
        c.Text(1, 2, "LEFT TO COLLECT " + Remaining.ToString("D2"), OricColor.White);
        if (Notice is string notice)
        {
            c.Fill(6, 84, Canvas.Width - 12, 12, OricColor.Black);
            c.TextCentered(86, notice, OricColor.Yellow);
        }
    }

    private void ShowNotice(string text)
    {
        _notice = text;
        _noticeTicks = 130;
    }
}
