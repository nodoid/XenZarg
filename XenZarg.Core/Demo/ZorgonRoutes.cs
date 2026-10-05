using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using XenZarg.Core.Oric;
using XenZarg.Core.Zorgon;
using XenZarg.Core.Zorgon.Missions;

namespace XenZarg.Core.Demo;

/// <summary>
/// The winning route through each Zorgon's Revenge screen, written as scripts that set the
/// controls one tick at a time. Each element yielded means "run one game tick now".
/// Used for the attract demo / store videos, and by the tests to prove every screen is winnable.
/// </summary>
public static class ZorgonRoutes
{
    public static IEnumerable<int> For(ZorgonMission mission, ScriptedControls c) => mission switch
    {
        SpiderMission m => Spider(m, c),
        ReptileMission m => Reptile(m, c),
        BirdMission m => Bird(m, c),
        BridgeScreen m => Bridge(m, c),
        KingScreen m => King(m, c),
        ShipMission m => Ship(m, c),
        _ => Array.Empty<int>(),
    };

    private static void Hold(Hero h, ScriptedControls c) => c.Pointer = new Vector2(h.X, 100);

    private static IEnumerable<int> WalkTo(Hero h, ScriptedControls c, float x, int limit = 3000)
    {
        c.Pointer = new Vector2(x, 100);
        for (int i = 0; i < limit && Math.Abs(h.X - x) > 0.6f && !h.IsDead; i++)
            yield return 0;
        Hold(h, c);
    }

    private static IEnumerable<int> Jump(Hero h, ScriptedControls c, int dir)
    {
        c.Pointer = new Vector2(h.X + dir * 60, 100);
        c.FirePressed = true;
        yield return 0;
        Hold(h, c);
    }

    private static IEnumerable<int> Until(Func<bool> condition, int limit = 6000)
    {
        for (int i = 0; i < limit && !condition(); i++)
            yield return 0;
    }

    /// <summary>Waits for a condition, jumping in the given direction the moment it holds.</summary>
    private static IEnumerable<int> JumpWhen(Hero h, ScriptedControls c, Func<bool> condition, int dir, Func<bool> done, int limit = 20000)
    {
        for (int i = 0; i < limit && !done() && !h.IsDead; i++)
        {
            if (h.Current == Hero.State.Standing && condition())
            {
                foreach (var t in Jump(h, c, dir))
                    yield return t;
            }
            else
            {
                yield return 0;
            }
        }
    }

    private static IEnumerable<int> Seq(params IEnumerable<int>[] parts)
    {
        foreach (var p in parts)
            foreach (var t in p)
                yield return t;
    }

    public static IEnumerable<int> Spider(SpiderMission m, ScriptedControls c)
    {
        var h = m.Hero;
        float last = m.RopeX;
        bool RopeComing(float limit, int dir)
        {
            bool ok = dir < 0 ? m.RopeX <= limit && m.RopeX < last : m.RopeX >= limit && m.RopeX > last;
            last = m.RopeX;
            return ok;
        }
        bool Standing() => h.Current == Hero.State.Standing;

        foreach (var t in Seq(
            WalkTo(h, c, 64),
            JumpWhen(h, c, () => RopeComing(82, -1), 1, () => h.Current == Hero.State.Riding),
            Until(() => m.RopeX >= SpiderMission.RopeRight - 0.5f),
            Jump(h, c, 1), Until(Standing),
            WalkTo(h, c, 205),
            JumpWhen(h, c, () => m.Lift.Direction < 0 && m.Lift.Y <= 190 && m.Lift.Y >= 183, 1, () => h.Carrier == m.Lift),
            Until(() => m.Lift.Y <= 80),
            Jump(h, c, -1), Until(Standing),
            WalkTo(h, c, 68),
            Jump(h, c, 0), Until(Standing),
            WalkTo(h, c, 205),
            JumpWhen(h, c, () => m.Lift.Direction > 0 && m.Lift.Y >= 62 && m.Lift.Y <= 66, 1, () => h.Carrier == m.Lift),
            Until(() => m.Lift.Y >= 184),
            Jump(h, c, -1), Until(Standing),
            WalkTo(h, c, 140)))
            yield return t;
        last = m.RopeX;
        foreach (var t in Seq(
            JumpWhen(h, c, () => RopeComing(128, 1), -1, () => h.Current == Hero.State.Riding),
            Until(() => m.RopeX <= SpiderMission.RopeLeft + 0.5f),
            Jump(h, c, -1), Until(Standing),
            WalkTo(h, c, 20),
            Until(() => m.Completed, 50)))
            yield return t;
    }

    public static IEnumerable<int> Reptile(ReptileMission m, ScriptedControls c)
    {
        var h = m.Hero;
        bool Standing() => h.Current == Hero.State.Standing;
        bool WhiteComing(int dir) => m.Shots.Any(s => s.Color == OricColor.White && !s.Bounced && s.Dir == dir &&
                                                      (dir < 0 ? s.X - h.X : h.X - s.X) is > 8 and < 16);
        foreach (var t in Seq(
            WalkTo(h, c, 100),
            JumpWhen(h, c, () => WhiteComing(-1), 0, () => !m.Q1.Alive),
            Until(Standing),
            WalkTo(h, c, 205),
            JumpWhen(h, c, () => m.Lift.Y <= 190 && m.Lift.Y >= 183, 1, () => h.Carrier == m.Lift),
            Until(() => m.Lift.Y <= 131),
            Jump(h, c, -1), Until(Standing),
            WalkTo(h, c, 14),
            Jump(h, c, 0), Until(Standing),
            WalkTo(h, c, 205),
            JumpWhen(h, c, () => m.Lift.Y <= 126 && m.Lift.Y >= 120, 1, () => h.Carrier == m.Lift),
            Until(() => m.Lift.Y <= 68),
            Jump(h, c, -1), Until(Standing),
            WalkTo(h, c, 130),
            JumpWhen(h, c, () => WhiteComing(1), 0, () => !m.Q2.Alive),
            Until(Standing),
            WalkTo(h, c, 20),
            Until(() => m.Completed, 50)))
            yield return t;
    }

    public static IEnumerable<int> Bird(BirdMission m, ScriptedControls c)
    {
        var h = m.Hero;
        bool Standing() => h.Current == Hero.State.Standing;
        bool BirdNear() => m.BirdActive && m.Bird.x <= 189 && m.Bird.x > 187;
        foreach (var t in Seq(
            WalkTo(h, c, 197),
            JumpWhen(h, c, BirdNear, 0, () => h.Current == Hero.State.Riding),
            Until(() => m.MiddleBridge),
            Jump(h, c, 0), Until(Standing),
            WalkTo(h, c, 225),
            WalkTo(h, c, 205),
            JumpWhen(h, c, BirdNear, 0, () => h.Current == Hero.State.Riding),
            Until(() => m.UpperBridge),
            Jump(h, c, 0), Until(Standing),
            WalkTo(h, c, 220),
            Until(() => m.Completed, 50)))
            yield return t;
    }

    public static IEnumerable<int> Bridge(BridgeScreen m, ScriptedControls c)
    {
        var h = m.Hero;
        foreach (var t in Seq(
            WalkTo(h, c, BridgeScreen.HeroLimitX + 10, 3000),
            Jump(h, c, 0),
            Until(() => m.Completed)))
            yield return t;
    }

    public static IEnumerable<int> King(KingScreen m, ScriptedControls c)
    {
        var h = m.Hero;
        for (int guard = 0; guard < 20 && m.Hits < KingScreen.HitsNeeded && !h.IsDead; guard++)
        {
            foreach (var t in Seq(
                WalkTo(h, c, m.RightArmed ? 225 : 15),
                Jump(h, c, 0),
                Until(() => h.Current == Hero.State.Standing),
                Until(() => m.Shots.Count == 0, 400)))
                yield return t;
        }
        foreach (var t in Until(() => m.Completed))
            yield return t;
    }

    /// <summary>Lines the ship up with the nearest alien (or the mothership) and keeps firing.</summary>
    public static IEnumerable<int> Ship(ShipMission m, ScriptedControls c)
    {
        for (int i = 0; i < 60000 && !m.Completed && !m.Failed; i++)
        {
            float targetY = m.MothershipActive
                ? m.Mothership.y
                : m.Aliens.Count > 0 ? m.Aliens.OrderBy(a => a.X).First().Y : 90;
            c.Pointer = new Vector2(0, targetY + 14);
            c.FirePressed = i % 8 == 0;
            yield return 0;
        }
    }
}
