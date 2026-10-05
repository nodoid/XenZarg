using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;
using XenZarg.Core.Xenon;
using XenZarg.Core.Zorgon;
using XenZarg.Core.Zorgon.Missions;

namespace XenZarg.Core.Demo;

/// <summary>Plays one Xenon 1 level on autopilot (attract mode and store captures).</summary>
public sealed class XenonDemoScene : Scene
{
    private readonly XenonWorld _world;
    private readonly XenonLevel _level;
    private readonly ScriptedControls _controls = new();
    private readonly XenonAutopilot _pilot;
    private readonly RefuelSequence _refuel;

    public XenonDemoScene(XenZargGame game, int level, ISoundPlayer sound, int seed = 1983, bool refuel = false) : base(game)
    {
        var session = new GameSession(5, 5190);
        session.AddScore(level * 1350);
        _world = new XenonWorld(session, _controls, sound, new Random(seed)) { Wave = level };
        _level = XenonScene.CreateLevel(level, _world);
        _level.Invulnerable = true;
        _pilot = new XenonAutopilot(_controls);
        if (refuel)
        {
            _world.Fuel = 40;
            _refuel = new RefuelSequence(_world, 110);
        }
        else
        {
            _world.Fuel = 120 + level * 15;
        }
    }

    protected override void Update()
    {
        _world.Tick++;
        _world.UpdateBlasts();
        if (_refuel != null)
        {
            // Steer for the gap in the next barrier, then the service bay.
            float target = RefuelSequence.BayX + RefuelSequence.BayWidth / 2f;
            for (int b = RefuelSequence.BarrierRows.Length - 1; b >= 0; b--)
            {
                if (_refuel.ShipY + PlayerShip.Sprite.Height >= RefuelSequence.BarrierRows[b])
                {
                    target = _refuel.GapX(b) + RefuelSequence.GapWidth / 2f;
                    break;
                }
            }
            _controls.Pointer = new Microsoft.Xna.Framework.Vector2(target, 0);
            _controls.FireHeld = Math.Abs(_refuel.ShipX + 5.5f - target) > 4;
            _refuel.Update();
            return;
        }
        _pilot.Steer(_level, _world);
        if (!_level.Completed)
            _level.Update();
        _controls.EndTick();
    }

    public override void Draw(Canvas c)
    {
        _world.DrawFuel(c);
        if (_refuel != null)
            _refuel.Draw(c);
        else
            _level.Draw(c);
        _world.DrawCommon(c);
        _world.DrawHud(c);
    }
}

/// <summary>Plays one Zorgon's Revenge screen along its winning route.</summary>
public sealed class ZorgonDemoScene : Scene
{
    private readonly ZorgonWorld _world;
    private readonly ZorgonMission _mission;
    private readonly ScriptedControls _controls = new();
    private readonly IEnumerator<int> _route;
    private readonly bool _heart;

    public ZorgonDemoScene(XenZargGame game, string screen, ISoundPlayer sound, int seed = 1983) : base(game)
    {
        var session = new GameSession(5, 5190);
        session.AddScore(9000 + seed % 7 * 1000);
        _world = new ZorgonWorld(session, _controls, sound, new Random(seed));
        _heart = screen == "heart";
        _mission = screen switch
        {
            "spider" => new SpiderMission(_world),
            "reptile" => new ReptileMission(_world),
            "ship" => new ShipMission(_world),
            "bird" => new BirdMission(_world),
            "bridge" => new BridgeScreen(_world, 2),
            _ => new KingScreen(_world),
        };
        _mission.Invulnerable = true;
        _world.Bonus = _mission.StartBonus;
        _route = ZorgonRoutes.For(_mission, _controls).GetEnumerator();
    }

    public ZorgonMission Mission => _mission;

    protected override void Update()
    {
        if (_heart)
            return;
        _world.Tick++;
        if (!_mission.Completed && !_mission.Failed)
        {
            _route.MoveNext();
            _mission.Update();
            if (_mission.BonusCountsDown)
                _world.CountDownBonus();
        }
        _controls.EndTick();
    }

    public override void Draw(Canvas c)
    {
        if (_heart)
        {
            c.Draw(SpriteData.ZCompletionHeart, 83, 59);
            _world.DrawHud(c, OricColor.Yellow, OricColor.Blue);
            return;
        }
        _mission.Draw(c);
        _world.DrawHud(c, _mission.HudPaper, _mission.HudInk);
    }
}
