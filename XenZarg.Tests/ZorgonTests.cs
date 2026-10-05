using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using XenZarg.Core.Demo;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;
using XenZarg.Core.Zorgon;
using XenZarg.Core.Zorgon.Missions;

namespace XenZarg.Tests;

public class HeroTests
{
    private static (ZorgonWorld w, FakeControls c) World()
    {
        var c = new FakeControls();
        return (new ZorgonWorld(new GameSession(1, 0), c, new RecordingSound(), new Random(1)), c);
    }

    [Fact]
    public void JumpRisesTwentyRowsAndLandsBack()
    {
        var (w, c) = World();
        var hero = new Hero(50, 100);
        var floor = new List<Surface> { new(0, 200, 100) };
        c.FirePressed = true;
        hero.Update(w, floor);
        c.FirePressed = false;
        float highest = hero.Feet;
        for (int i = 0; i < 100 && hero.Current != Hero.State.Standing; i++)
        {
            hero.Update(w, floor);
            highest = Math.Min(highest, hero.Feet);
        }
        Assert.Equal(Hero.State.Standing, hero.Current);
        Assert.Equal(100 - Hero.JumpRows, highest, 1);
        Assert.Equal(100, hero.Feet);
    }

    [Fact]
    public void WalkingOffAnEdgeIsFatal()
    {
        var (w, c) = World();
        var hero = new Hero(40, 100);
        var floor = new List<Surface> { new(0, 50, 100), new(0, 239, 130) };
        c.MoveX = 1;
        for (int i = 0; i < 400 && !hero.IsDead; i++)
            hero.Update(w, floor);
        Assert.True(hero.IsDead);
    }

    [Fact]
    public void LandingTooFarBelowThePeakIsFatal()
    {
        var (w, c) = World();
        var hero = new Hero(48, 100);
        var floors = new List<Surface> { new(0, 50, 100), new(0, 239, 140) };
        c.MoveX = 1;
        c.FirePressed = true;
        hero.Update(w, floors);
        c.FirePressed = false;
        for (int i = 0; i < 400 && hero.Current != Hero.State.Standing && !hero.IsDead; i++)
            hero.Update(w, floors);
        Assert.True(hero.IsDead);
    }

    [Fact]
    public void ShortDropIsSafe()
    {
        var (w, c) = World();
        var hero = new Hero(48, 100);
        var floors = new List<Surface> { new(0, 50, 100), new(0, 239, 110) };
        c.MoveX = 1;
        c.FirePressed = true;
        hero.Update(w, floors);
        c.FirePressed = false;
        for (int i = 0; i < 400 && hero.Current != Hero.State.Standing && !hero.IsDead; i++)
            hero.Update(w, floors);
        Assert.Equal(Hero.State.Standing, hero.Current);
        Assert.Equal(110, hero.Feet);
    }

    [Fact]
    public void LandingInTheRiverIsFatal()
    {
        var (w, c) = World();
        var hero = new Hero(70, 100);
        var floors = new List<Surface> { new(0, 71, 100), new(72, 137, 103, Deadly: true) };
        c.MoveX = 1;
        c.FirePressed = true;
        hero.Update(w, floors);
        c.FirePressed = false;
        for (int i = 0; i < 400 && !hero.IsDead; i++)
            hero.Update(w, floors);
        Assert.True(hero.IsDead);
    }

    [Fact]
    public void WalkFramesComeFromThePreShiftedOricSprites()
    {
        var hero = new Hero(60, 100);
        var seen = new HashSet<object>();
        for (int x = 60; x < 66; x++)
        {
            hero.X = x;
            seen.Add(hero.Frame().sprite);
        }
        Assert.Equal(6, seen.Count);
    }
}

public class ZorgonCampaignTests
{
    [Fact]
    public void DoorsHideAllFourMissions()
    {
        var c = new ZorgonCampaign(new Random(3));
        var kinds = Enumerable.Range(0, 4).Select(c.MissionBehind).ToHashSet();
        Assert.Equal(4, kinds.Count);
    }

    [Fact]
    public void CompletedDoorsCannotBeChosenAgain()
    {
        var c = new ZorgonCampaign(new Random(3));
        Assert.True(c.Choose(2));
        c.MissionWon(2);
        Assert.False(c.Choose(2));
        Assert.Equal(3, c.NextOpenDoor(1, 1));
    }

    [Fact]
    public void FourBridgePartsCompleteTheBridge()
    {
        var c = new ZorgonCampaign(new Random(3));
        for (int i = 0; i < 3; i++)
            c.BridgePartLaid();
        Assert.False(c.BridgeComplete);
        c.BridgePartLaid();
        c.BridgePartLaid();
        Assert.True(c.BridgeComplete);
        Assert.Equal(4, c.PartsLaid);
    }

    [Fact]
    public void BonusCountsDownToOneThousand()
    {
        var w = new ZorgonWorld(new GameSession(1, 0), new FakeControls(), null, new Random(1)) { Bonus = 1100 };
        for (int t = 1; t <= 500; t++)
        {
            w.Tick = t;
            w.CountDownBonus();
        }
        Assert.Equal(1000, w.Bonus);
    }
}

/// <summary>Plays each screen along the scripted winning route, proving it can be completed.</summary>
public class ZorgonRouteTests
{
    private static ZorgonMission Run(Func<ZorgonWorld, ZorgonMission> create, int seed = 7, bool invulnerable = true)
    {
        var c = new ScriptedControls();
        var w = new ZorgonWorld(new GameSession(1, 0), c, new RecordingSound(), new Random(seed));
        var m = create(w);
        m.Invulnerable = invulnerable;
        foreach (var _ in ZorgonRoutes.For(m, c))
        {
            w.Tick++;
            m.Update();
            c.EndTick();
            Assert.False(m.Failed, $"died on the route ({m.GetType().Name})");
            if (m.Completed)
                break;
        }
        for (int i = 0; i < 300 && !m.Completed && !m.Failed; i++)
        {
            w.Tick++;
            m.Update();
        }
        return m;
    }

    [Fact]
    public void SpiderMissionCanBeCompleted()
    {
        var m = (SpiderMission)Run(w => new SpiderMission(w));
        Assert.True(m.ButtonPressed);
        Assert.True(m.Completed);
    }

    [Fact]
    public void ReptileMissionCanBeCompleted()
    {
        var m = (ReptileMission)Run(w => new ReptileMission(w));
        Assert.False(m.Q1.Alive);
        Assert.False(m.Q2.Alive);
        Assert.True(m.SwitchThrown);
        Assert.True(m.Completed);
    }

    [Fact]
    public void BirdMissionCanBeCompleted()
    {
        var m = (BirdMission)Run(w => new BirdMission(w));
        Assert.True(m.UpperBridge);
        Assert.True(m.Completed);
    }

    [Fact]
    public void BridgeScreenLaysAPart()
    {
        var m = (BridgeScreen)Run(w => new BridgeScreen(w, 2));
        Assert.True(m.Completed);
        Assert.Equal(3, m.PartsLaid);
    }

    [Fact]
    public void KingFallsAfterThreeLeftCannonHits()
    {
        var m = (KingScreen)Run(w => new KingScreen(w));
        Assert.Equal(KingScreen.HitsNeeded, m.Hits);
        Assert.True(m.Completed);
    }

    [Fact]
    public void ShipMissionWinsByShootingTheMothership()
    {
        // The ship has no shield, so allow a few attempts: a run can be lost to an alien's shot.
        for (int seed = 1; seed <= 10; seed++)
        {
            var c = new ScriptedControls();
            var w = new ZorgonWorld(new GameSession(1, 0), c, new RecordingSound(), new Random(seed));
            var m = new ShipMission(w);
            foreach (var _ in ZorgonRoutes.Ship(m, c))
            {
                w.Tick++;
                m.Update();
                c.EndTick();
            }
            for (int i = 0; i < 200 && !m.Completed && !m.Failed; i++)
            {
                w.Tick++;
                m.Update();
            }
            if (m.Completed)
                return;
        }
        Assert.Fail("the ship mission was never won in ten attempts");
    }

    [Theory]
    [InlineData(MissionKind.Spider)]
    [InlineData(MissionKind.Reptile)]
    [InlineData(MissionKind.Ship)]
    [InlineData(MissionKind.Bird)]
    public void MissionsSurviveRandomInput(MissionKind kind)
    {
        var c = new ScriptedControls();
        var w = new ZorgonWorld(new GameSession(9, 0), c, new RecordingSound(), new Random((int)kind));
        var m = ZorgonScene.CreateMission(kind, w);
        var rng = new Random(5);
        for (int i = 0; i < 4000 && !m.Failed && !m.Completed; i++)
        {
            c.MoveX = rng.Next(3) - 1;
            c.MoveY = rng.Next(3) - 1;
            c.FirePressed = rng.Next(20) == 0;
            w.Tick++;
            m.Update();
        }
    }
}
