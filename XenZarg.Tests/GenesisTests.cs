using System;
using System.Linq;
using Microsoft.Xna.Framework;
using XenZarg.Core.Demo;
using XenZarg.Core.Genesis;
using XenZarg.Core.Genesis.Missions;
using XenZarg.Core.Session;

namespace XenZarg.Tests;

public class GenesisCampaignTests
{
    [Fact]
    public void RadonStaysLockedUntilTheFiveOuterPlanetsAreCleared()
    {
        var c = new GenesisCampaign();
        Assert.False(c.RadonUnlocked);
        Assert.False(c.CanChoose(Planet.Radon));
        foreach (var p in GenesisPlanets.All)
            if (p != Planet.Radon)
                c.Complete(p);
        Assert.True(c.RadonUnlocked);
        Assert.True(c.CanChoose(Planet.Radon));
        Assert.Equal(5, c.ClearedCount);
    }

    [Fact]
    public void NextOpenSkipsClearedPlanets()
    {
        var c = new GenesisCampaign();
        c.Complete(Planet.Rundus);
        Assert.Equal(Planet.Xer, c.NextOpen(Planet.Rundus));
        foreach (var p in GenesisPlanets.All)
            if (p != Planet.Radon)
                c.Complete(p);
        Assert.Equal(Planet.Radon, c.NextOpen(Planet.Terra));
    }

    [Fact]
    public void EveryPlanetHasAMissionImplementation()
    {
        foreach (var p in GenesisPlanets.All)
        {
            var w = World();
            var m = GenesisScene.CreateMission(p, w);
            Assert.Equal(p, m.Planet);
            Assert.True(m.Goal > 0);
        }
    }

    private static GenesisWorld World() =>
        new(new GameSession(1, 0), new ScriptedControls(), new RecordingSound(), new Random(7));
}

public class HyperdriveTests
{
    [Fact]
    public void HoldingTheSpeedBandFillsTheJumpGauge()
    {
        var c = new ScriptedControls();
        var h = new Hyperdrive(new Random(3), new RecordingSound());
        for (int i = 0; i < 5000 && !h.Completed && !h.Failed; i++)
        {
            c.MoveY = h.Speed < 0.55f ? -1 : h.Speed > 0.64f ? 1 : 0;
            h.Update(c);
        }
        Assert.True(h.Completed);
        Assert.False(h.Failed);
        Assert.Equal(1f, h.Charge, 2);
    }

    [Fact]
    public void TheMouseOverTheGaugeSetsTheSpeed()
    {
        var c = new ScriptedControls
        {
            Pointer = new Vector2(Hyperdrive.BarX + (int)(0.54f * Hyperdrive.BarWidth), Hyperdrive.SpeedBarY + 2),
        };
        var h = new Hyperdrive(new Random(3), new RecordingSound());
        for (int i = 0; i < 400; i++)
            h.Update(c);
        Assert.InRange(h.Speed, 0.50f, 0.58f);
        Assert.True(h.InBand);
        Assert.True(h.Charge > 0.5f);
    }

    [Fact]
    public void MeteorsStreamInFromTheRightAndTravelLeft()
    {
        var c = new ScriptedControls();
        var h = new Hyperdrive(new Random(3), new RecordingSound());
        for (int i = 0; i < 40; i++)
            h.Update(c);
        Assert.NotEmpty(h.Meteors);
        var m = h.Meteors[^1];
        Assert.True(m.X > 200, $"meteor entered at x={m.X}");
        float before = m.X;
        h.Update(c);
        Assert.True(m.X < before);
    }

    [Fact]
    public void TheJumpCounterFallsAsTheChargeFills()
    {
        var c = new ScriptedControls();
        var h = new Hyperdrive(new Random(3), new RecordingSound());
        Assert.Equal(7, h.SecondsLeft);
        for (int i = 0; i < 2000 && !h.Completed; i++)
        {
            c.MoveY = h.Speed < 0.55f ? -1 : h.Speed > 0.64f ? 1 : 0;
            h.Update(c);
            Assert.InRange(h.SecondsLeft, 0, 7);
        }
        Assert.True(h.Completed);
        Assert.Equal(0, h.SecondsLeft);
    }

    [Fact]
    public void TheMouseAwayFromTheGaugeDoesNotSetTheSpeed()
    {
        var c = new ScriptedControls { Pointer = new Vector2(120, 99) };
        var h = new Hyperdrive(new Random(3), new RecordingSound());
        for (int i = 0; i < 20; i++)
            h.Update(c);
        // Only the engine's slow drift, not the 0.54 the gauge would ask for.
        Assert.InRange(h.Speed, 0.12f, 0.2f);
        Assert.False(h.InBand);
    }

    [Fact]
    public void StallingTheEngineFailsTheRun()
    {
        var c = new ScriptedControls { MoveY = 1 };
        var h = new Hyperdrive(new Random(3), new RecordingSound());
        for (int i = 0; i < 400 && !h.Failed; i++)
            h.Update(c);
        Assert.True(h.Failed);
        h.Reset();
        Assert.False(h.Failed);
        Assert.Equal(0f, h.Charge);
    }
}

public class GenesisMissionTests
{
    private static GenesisWorld World(int seed = 7)
    {
        var w = new GenesisWorld(new GameSession(3, 0), new ScriptedControls(), new RecordingSound(), new Random(seed));
        return w;
    }

    [Fact]
    public void RundusCanBeClearedWithTheTeleportBeam()
    {
        var w = World();
        var c = (ScriptedControls)w.Controls;
        var m = new CaptureMission(w, Planet.Rundus) { Invulnerable = true };
        for (int i = 0; i < 40000 && !m.Completed && !m.Failed; i++)
        {
            AimAtCaptureTarget(m, c);
            w.Tick++;
            m.Update();
            c.EndTick();
        }
        Assert.True(m.Completed, $"only caught {m.Specimens}/{m.Goal}");
        Assert.Equal(m.Goal, m.Specimens);
        Assert.Equal(0, m.Remaining);
    }

    [Fact]
    public void TerraCanBeClearedWithTheTeleportBeam()
    {
        var w = World();
        var c = (ScriptedControls)w.Controls;
        var m = new CaptureMission(w, Planet.Terra) { Invulnerable = true };
        for (int i = 0; i < 60000 && !m.Completed && !m.Failed; i++)
        {
            AimAtCaptureTarget(m, c);
            w.Tick++;
            m.Update();
            c.EndTick();
        }
        Assert.True(m.Completed, $"only caught {m.Specimens}/{m.Goal}");
    }

    [Fact]
    public void SomtanaCanBeClearedWithTheTeleportBeam()
    {
        var w = World(11);
        var c = (ScriptedControls)w.Controls;
        var m = new CaptureMission(w, Planet.Somtana) { Invulnerable = true };
        for (int i = 0; i < 60000 && !m.Completed && !m.Failed; i++)
        {
            AimAtCaptureTarget(m, c);
            w.Tick++;
            m.Update();
            c.EndTick();
        }
        Assert.True(m.Completed, $"only caught {m.Specimens}/{m.Goal}");
    }

    [Fact]
    public void MissingTwoBarrelsCostsAShield()
    {
        var w = World();
        var m = new CaptureMission(w, Planet.Rundus);
        int before = w.Session.Bases;
        // Put two barrels just off the edge and let them leave.
        m.Barrels.Add(new Barrel { X = 239, Y = 40, Dir = 1, Speed = 2 });
        m.Barrels.Add(new Barrel { X = 239, Y = 80, Dir = 1, Speed = 2 });
        for (int i = 0; i < 5; i++)
        {
            w.Tick++;
            m.Update();
        }
        Assert.Equal(before - 1, w.Session.Bases);
        // Losing the shield blows the probe up rather than only playing a sound.
        Assert.InRange(w.BlastAge, 0, GenesisWorld.ShipDownTicks - 1);
        Assert.True(w.ShipHidden);
        Assert.Contains("SHIELD", m.Notice);
    }

    [Fact]
    public void TheCaptureScreenCountsDownWhatIsLeftToCollect()
    {
        var w = World();
        var m = new CaptureMission(w, Planet.Rundus);
        Assert.Equal(m.Goal, m.Remaining);
        Assert.True(m.Remaining > 0);
    }

    [Fact]
    public void TheTeleportBeamOnlyFlashesWhenFired()
    {
        var w = World();
        var c = (ScriptedControls)w.Controls;
        var m = new CaptureMission(w, Planet.Rundus);
        Assert.False(m.BeamOn);

        c.FirePressed = true;
        w.Tick++;
        m.Update();
        c.EndTick();
        Assert.True(m.BeamOn);

        m.Transits.Clear();
        for (int i = 0; i <= CaptureMission.BeamTicks; i++)
        {
            w.Tick++;
            m.Update();
        }
        Assert.False(m.BeamOn);
    }

    [Fact]
    public void XerYieldsHeadsToTheBoomerang()
    {
        var w = World();
        var c = (ScriptedControls)w.Controls;
        var m = new WormMission(w) { Invulnerable = true };
        for (int i = 0; i < 40000 && !m.Completed && !m.Failed; i++)
        {
            var worm = m.Worms.OrderByDescending(x => x.Length).FirstOrDefault();
            if (worm == null)
                c.Pointer = null;
            else
                c.Pointer = new Vector2(worm.HeadX + 3, 100);
            c.FirePressed = !m.Boomerang.Active && worm != null;
            w.Tick++;
            m.Update();
            c.EndTick();
        }
        Assert.True(m.Completed, $"only caught {m.Heads}/{WormMission.HeadsNeeded} heads");
        Assert.True(m.Heads >= WormMission.HeadsNeeded);
    }

    [Fact]
    public void RadonIsSurvivedByDodgingTheZorgonStorm()
    {
        var w = World();
        var c = (ScriptedControls)w.Controls;
        var m = new RadonBattle(w) { Invulnerable = true };
        for (int i = 0; i < 60000 && !m.Completed && !m.Failed; i++)
        {
            // Move to the clearest part of the screen rather than firing: Radon has no weapon.
            float bestX = 120;
            int bestClearance = int.MinValue;
            for (int x = 4; x <= 220; x += 8)
            {
                int clearance = int.MaxValue;
                foreach (var z in m.Enemies)
                    clearance = Math.Min(clearance, Math.Abs((int)z.X - x) + (int)z.Y);
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestX = x;
                }
            }
            c.Pointer = new Vector2(bestX, 100);
            w.Tick++;
            m.Update();
            c.EndTick();
        }
        Assert.True(m.Completed, $"reached wave {m.Wave}/{RadonBattle.Waves}");
    }

    [Theory]
    [InlineData(Planet.Rundus)]
    [InlineData(Planet.Xer)]
    [InlineData(Planet.Somtana)]
    [InlineData(Planet.Nuciro)]
    [InlineData(Planet.Terra)]
    [InlineData(Planet.Radon)]
    public void MissionsSurviveRandomInput(Planet planet)
    {
        var w = World((int)planet);
        var c = (ScriptedControls)w.Controls;
        var m = GenesisScene.CreateMission(planet, w);
        var rng = new Random(5);
        for (int i = 0; i < 3000 && !m.Failed && !m.Completed; i++)
        {
            c.MoveX = rng.Next(3) - 1;
            c.MoveY = rng.Next(3) - 1;
            c.FirePressed = rng.Next(8) == 0;
            c.LeftPressed = rng.Next(60) == 0;
            c.RightPressed = rng.Next(60) == 0;
            c.UpPressed = rng.Next(60) == 0;
            c.DownPressed = rng.Next(60) == 0;
            w.Tick++;
            m.Update();
            c.EndTick();
        }
    }

    private static void AimAtCaptureTarget(CaptureMission m, ScriptedControls c)
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
            c.Pointer = new Vector2(x, 100);
        c.FirePressed = target.HasValue && m.Transits.Count < CaptureMission.TransportLimit &&
                        Math.Abs(m.Ship.CentreX - target.Value) <= 2;
    }
}
