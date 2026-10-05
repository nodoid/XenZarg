using System;
using System.Linq;
using Microsoft.Xna.Framework;
using XenZarg.Core.Graphics;
using XenZarg.Core.Xenon;

namespace XenZarg.Tests;

public class XenonWorldTests
{
    [Fact]
    public void OnlyOneLaserBoltAtATime()
    {
        var laser = new Laser();
        Assert.True(laser.TryFire(10, 100));
        Assert.False(laser.TryFire(20, 100));
        for (int i = 0; i < 40; i++)
            laser.Update();
        Assert.False(laser.Active);
        Assert.True(laser.TryFire(20, 100));
    }

    [Fact]
    public void FuelBurnsSlowlyAndRefuelOpensBelowYellow()
    {
        var w = TestWorld.Create(out _);
        Assert.False(w.CanRefuel);
        for (int t = 1; t <= XenonWorld.FuelBurnTicks * 10; t++)
        {
            w.Tick = t;
            w.Burn();
        }
        Assert.Equal(XenonWorld.MaxFuel - 10, w.Fuel);
        w.Fuel = XenonWorld.FuelYellow - 1;
        Assert.True(w.CanRefuel);
    }

    [Fact]
    public void BombsHitThePlayerAndExpireAtTheFloor()
    {
        var w = TestWorld.Create(out _);
        var bomb = Sprite.Of(SpriteData.BombWhiteRed);
        var ship = PlayerShip.Sprite;
        w.DropBomb(105, 170, bomb, 3f);
        bool hit = false;
        for (int i = 0; i < 20 && !hit; i++)
            hit = w.UpdateBombs(ship, 100, PlayerShip.Top);
        Assert.True(hit);

        w.DropBomb(10, 190, bomb, 3f);
        for (int i = 0; i < 10; i++)
            w.UpdateBombs(ship, 200, PlayerShip.Top);
        Assert.Empty(w.Bombs);
    }

    [Fact]
    public void ShipFollowsTheMouseAtLimitedSpeed()
    {
        var w = TestWorld.Create(out var c);
        var ship = new PlayerShip { X = 0 };
        c.Pointer = new Vector2(200, 100);
        ship.Update(w, false);
        Assert.Equal(PlayerShip.Speed, ship.X, 3);
        for (int i = 0; i < 200; i++)
            ship.Update(w, false);
        Assert.Equal(200 - PlayerShip.Sprite.Width / 2f, ship.X, 3);
    }

    [Fact]
    public void ShipStaysOnScreen()
    {
        var w = TestWorld.Create(out var c);
        var ship = new PlayerShip();
        c.MoveX = 1;
        for (int i = 0; i < 300; i++)
            ship.Update(w, false);
        Assert.Equal(Canvas.Width - PlayerShip.Sprite.Width, ship.X);
        c.MoveX = -1;
        for (int i = 0; i < 300; i++)
            ship.Update(w, false);
        Assert.Equal(0, ship.X);
    }
}

public class XenonLevelTests
{
    [Fact]
    public void LevelsAreCreatedInTheOriginalOrder()
    {
        var w = TestWorld.Create(out _);
        Assert.IsType<BirdAardLevel>(XenonScene.CreateLevel(1, w));
        Assert.IsType<AardLevel>(XenonScene.CreateLevel(2, w));
        Assert.IsType<MeteorLevel>(XenonScene.CreateLevel(3, w));
        Assert.IsType<RadonLevel>(XenonScene.CreateLevel(4, w));
        Assert.IsType<BattleStarLevel>(XenonScene.CreateLevel(5, w));
    }

    [Fact]
    public void BirdAardsAreWorth150AndClearingThemCompletesTheLevel()
    {
        var w = TestWorld.Create(out _, skill: 1);
        var level = new BirdAardLevel(w);
        int guard = 0;
        while (!level.Completed && guard++ < 5000)
        {
            var target = level.Birds.FirstOrDefault(b => b.Alive && !b.Diving && !b.Returning);
            if (target != null && !w.Laser.Active)
            {
                // Place a bolt right under the bird, as if fired from below.
                w.Laser.TryFire(target.X + 9, target.Y + 10);
            }
            TestWorld.Step(w, level);
            if (level.PlayerHit)
                level.Respawn();
        }
        Assert.True(level.Completed);
        Assert.Equal(BirdAardLevel.BirdCount * BirdAardLevel.Points, w.Session.Score);
    }

    [Fact]
    public void LargeAardClonesIntoAWandererAndADiver()
    {
        var w = TestWorld.Create(out _, skill: 1);
        var level = new AardLevel(w);
        var large = level.Aards[0];
        var sprite = Sprite.Of(SpriteData.LargeAard1);
        w.Laser.Active = true;
        w.Laser.X = large.X + sprite.Width / 2f + large.Vx;
        w.Laser.Y = large.Y + sprite.Height / 2f + Laser.Speed + large.Vy;
        TestWorld.Step(w, level);
        Assert.Equal(AardLevel.LargePoints, w.Session.Score);
        Assert.Contains(level.Aards, a => a.Kind == AardLevel.Kind.Small);
        Assert.Contains(level.Aards, a => a.Kind == AardLevel.Kind.Diver);
        Assert.Equal(AardLevel.LargeCount + 1, level.Aards.Count);
    }

    [Fact]
    public void DiverExplodesIntoFatalRadiation()
    {
        var w = TestWorld.Create(out _, skill: 1);
        var level = new AardLevel(w);
        var large = level.Aards[0];
        w.Laser.Active = true;
        w.Laser.X = large.X + 9 + large.Vx;
        w.Laser.Y = large.Y + 9 + Laser.Speed + large.Vy;
        TestWorld.Step(w, level);

        // Stand still under the diver: it homes in and its radiation kills.
        int guard = 0;
        while (!level.PlayerHit && guard++ < 2000)
            TestWorld.Step(w, level);
        Assert.True(level.PlayerHit);
    }

    [Fact]
    public void MeteorLevelDisablesTheLaserAndScoresTenPerMeteor()
    {
        var w = TestWorld.Create(out var c, skill: 1);
        var level = new MeteorLevel(w);
        c.FireHeld = true;
        int guard = 0;
        while (!level.Completed && guard++ < 20000)
        {
            // Dodge: steer away from the nearest meteor above the ship.
            var threat = level.Meteors.Where(m => m.Y > 120).OrderByDescending(m => m.Y).FirstOrDefault();
            c.MoveX = threat == null ? 0 : (threat.X + 5 < level.Ship.CentreX ? 1 : -1);
            TestWorld.Step(w, level);
            Assert.False(w.Laser.Active);
            if (level.PlayerHit)
                level.Respawn();
        }
        Assert.True(level.Completed);
        Assert.Equal(level.Passed * MeteorLevel.PointsPerMeteor, w.Session.Score);
    }

    [Theory]
    [InlineData(1, 0, 10)]
    [InlineData(5, 0, 20)]
    [InlineData(9, 0, 30)]
    [InlineData(9, 1, 35)]
    public void ParatronsRequiredDependOnSkill(int skill, int cycle, int expected) =>
        Assert.Equal(expected, RadonLevel.RequiredKills(skill, cycle));

    [Fact]
    public void ParatronsLandAndBecomeTroopers()
    {
        var w = TestWorld.Create(out var c, skill: 1);
        var level = new RadonLevel(w);
        c.Pointer = new Vector2(0, 0); // keep the commander far left, out of the way
        int guard = 0;
        while (level.Troopers.Count == 0 && guard++ < 5000)
        {
            TestWorld.Step(w, level);
            if (level.PlayerHit)
                level.Respawn();
        }
        Assert.NotEmpty(level.Troopers);
    }

    [Theory]
    [InlineData(0, 10000)]
    [InlineData(50 * 10, 9000)]
    [InlineData(50 * 1000, 1000)]
    public void BattleStarBonusFallsWithTime(int ticks, int bonus) =>
        Assert.Equal(bonus, BattleStarLevel.BonusFor(ticks));

    [Fact]
    public void ReactorIsShieldedByTheForceField()
    {
        var w = TestWorld.Create(out _, skill: 1);
        var level = new BattleStarLevel(w);
        int cell = level.Field.Length / 2;
        Assert.True(level.Field[cell]);
        w.Laser.Active = true;
        w.Laser.X = BattleStarLevel.HullX + cell * BattleStarLevel.FieldCell + 1;
        w.Laser.Y = BattleStarLevel.FieldY + Laser.Speed;
        TestWorld.Step(w, level);
        Assert.False(level.Field[cell]);
        Assert.False(level.Destroyed);
    }

    [Fact]
    public void HittingTheReactorDestroysTheBattleStar()
    {
        var w = TestWorld.Create(out _, skill: 1);
        var level = new BattleStarLevel(w);
        Array.Fill(level.Field, false);
        foreach (var g in level.Guards)
            g.Alive = false;
        int guard = 0;
        while (!level.Destroyed && guard++ < 50)
        {
            w.Laser.Active = true;
            w.Laser.X = level.ReactorX + BattleStarLevel.ReactorWidth / 2f;
            w.Laser.Y = level.ReactorY + Laser.Speed + 2;
            TestWorld.Step(w, level);
        }
        Assert.True(level.Destroyed);
        Assert.InRange(level.CompletionBonus, BattleStarLevel.MinBonus, BattleStarLevel.MaxBonus);
        while (!level.Completed && guard++ < 1000)
            TestWorld.Step(w, level);
        Assert.True(level.Completed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void LevelsSurviveRandomPlay(int number)
    {
        var w = TestWorld.Create(out var c, skill: 9, seed: number);
        var level = XenonScene.CreateLevel(number, w);
        var rng = new Random(number);
        for (int i = 0; i < 3000 && !level.Completed; i++)
        {
            c.MoveX = rng.Next(3) - 1;
            c.FirePressed = rng.Next(4) == 0;
            TestWorld.Step(w, level);
            if (level.PlayerHit)
                level.Respawn();
            Assert.InRange(level.ShipX, 0, Canvas.Width);
        }
    }
}

public class RefuelTests
{
    [Fact]
    public void DockingUnderTheBayRefillsTheTank()
    {
        var w = TestWorld.Create(out var c);
        w.Fuel = 20;
        var refuel = new RefuelSequence(w, 100);
        int guard = 0;
        while (refuel.Current != RefuelSequence.State.Done && guard++ < 5000)
        {
            // Thread the gap in the next barrier, then line up with the bay.
            float target = RefuelSequence.BayX + RefuelSequence.BayWidth / 2f;
            for (int b = RefuelSequence.BarrierRows.Length - 1; b >= 0; b--)
            {
                // The lowest barrier the ship has not yet cleared.
                if (refuel.ShipY + PlayerShip.Sprite.Height >= RefuelSequence.BarrierRows[b])
                {
                    target = refuel.GapX(b) + RefuelSequence.GapWidth / 2f;
                    break;
                }
            }
            c.Pointer = new Vector2(target, 0);
            // Brake until lined up with the gap.
            c.FireHeld = Math.Abs(refuel.ShipX + PlayerShip.Sprite.Width / 2f - target) > 4;
            w.Tick++;
            refuel.Update();
        }
        Assert.False(refuel.Crashed);
        Assert.Equal(XenonWorld.MaxFuel, w.Fuel);
    }

    [Fact]
    public void FlyingIntoLightningCrashes()
    {
        var w = TestWorld.Create(out var c);
        var refuel = new RefuelSequence(w, 100);
        int guard = 0;
        while (refuel.Current == RefuelSequence.State.Climbing && guard++ < 5000)
        {
            // Hug the side furthest from the gap.
            int gap = refuel.GapX(RefuelSequence.BarrierRows.Length - 1);
            c.Pointer = new Vector2(gap > 105 ? 0 : 239, 0);
            w.Tick++;
            refuel.Update();
        }
        Assert.True(refuel.Crashed);
    }
}
