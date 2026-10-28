using System;
using System.Linq;
using Microsoft.Xna.Framework;
using XenZarg.Core.Audio;
using XenZarg.Core.Genesis;
using XenZarg.Core.Genesis.Missions;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;

namespace XenZarg.Core.Demo;

/// <summary>Plays one Xenon 3 planet on autopilot for the attract mode and store captures.</summary>
public sealed class GenesisDemoScene : Scene
{
    private readonly GenesisWorld _world;
    private readonly GenesisMission _mission;
    private readonly ScriptedControls _controls = new();

    public GenesisDemoScene(XenZargGame game, Planet planet, ISoundPlayer sound, int seed = 1985) : base(game)
    {
        var session = new GameSession(5, 5190);
        session.AddScore(2000 + (int)planet * 1450);
        _world = new GenesisWorld(session, _controls, sound, new Random(seed));
        _mission = GenesisScene.CreateMission(planet, _world);
        _mission.Invulnerable = true;
    }

    public GenesisMission Mission => _mission;

    protected override void Update()
    {
        _world.Tick++;
        if (_mission.Completed || _mission.Failed)
            return;

        switch (_mission)
        {
            case CaptureMission m:
                AimAtCapture(m);
                break;
            case WormMission m:
                AimAtWorms(m);
                break;
            case RadonBattle m:
                AimAtRadon(m);
                break;
        }
        _mission.Update();
        _controls.EndTick();
    }

    private void AimAtCapture(CaptureMission m)
    {
        float? target = null;
        int bestY = int.MinValue;
        foreach (var b in m.Barrels)
            if (b.Y > bestY)
            {
                bestY = b.Y;
                target = b.X + 3.5f;
            }
        foreach (var creature in m.Creatures)
            if (creature.Y > bestY)
            {
                bestY = creature.Y;
                target = creature.X + creature.Sprite.Width / 2f;
            }

        if (target is float x)
            _controls.Pointer = new Vector2(x, 100);
        _controls.FirePressed = target.HasValue && m.Transits.Count < CaptureMission.TransportLimit &&
                                Math.Abs(m.Ship.CentreX - target.Value) <= 2;
    }

    private void AimAtWorms(WormMission m)
    {
        var worm = m.Worms.OrderByDescending(w => w.Length).FirstOrDefault();
        if (worm == null)
        {
            _controls.Pointer = null;
            _controls.FirePressed = false;
            return;
        }
        _controls.Pointer = new Vector2(worm.HeadX + 3, 100);
        _controls.FirePressed = !m.Boomerang.Active;
    }

    private void AimAtRadon(RadonBattle m)
    {
        // Radon is a dodge: choose the part of the screen furthest from the storm.
        float target = 120;
        int best = int.MinValue;
        for (int x = 4; x <= 220; x += 8)
        {
            int clearance = int.MaxValue;
            foreach (var z in m.Enemies)
                clearance = Math.Min(clearance, Math.Abs((int)z.X - x) + (int)z.Y);
            if (clearance > best)
            {
                best = clearance;
                target = x;
            }
        }
        _controls.Pointer = new Vector2(target, 100);
        _controls.FirePressed = false;
    }

    public override void Draw(Canvas c)
    {
        _mission.Draw(c);
        _world.DrawHud(c, _mission.Specimens);
    }
}

/// <summary>The hyperspace run on autopilot.</summary>
public sealed class GenesisHyperDemoScene : Scene
{
    private readonly GenesisWorld _world;
    private readonly Hyperdrive _hyper;
    private readonly ScriptedControls _controls = new();

    public GenesisHyperDemoScene(XenZargGame game, ISoundPlayer sound) : base(game)
    {
        _world = new GenesisWorld(new GameSession(5, 5190), _controls, sound, new Random(1985));
        _hyper = new Hyperdrive(new Random(7), sound);
    }

    protected override void Update()
    {
        _world.Tick++;
        _controls.MoveY = _hyper.Speed < 0.55f ? -1 : _hyper.Speed > 0.64f ? 1 : 0;
        _hyper.Update(_controls);
        _controls.EndTick();
    }

    public override void Draw(Canvas c)
    {
        _hyper.Draw(c);
        _world.DrawHud(c, 0);
    }
}
