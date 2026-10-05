using System;
using System.Linq;
using Microsoft.Xna.Framework;
using XenZarg.Core.Graphics;
using XenZarg.Core.Xenon;

namespace XenZarg.Core.Demo;

/// <summary>A simple pilot for the Xenon 1 attract demo: lines up under targets and fires, dodging what it can.</summary>
public sealed class XenonAutopilot
{
    private readonly ScriptedControls _c;

    public XenonAutopilot(ScriptedControls controls)
    {
        _c = controls;
    }

    public void Steer(XenonLevel level, XenonWorld world)
    {
        float shipX = level.ShipX;
        float? target = level switch
        {
            BirdAardLevel b => b.Birds.Where(x => x.Alive).OrderByDescending(x => x.Y).Select(x => (float?)(x.X + 9)).FirstOrDefault(),
            AardLevel a => a.Aards.OrderByDescending(x => x.Y).Select(x => (float?)(x.X + 9)).FirstOrDefault(),
            RadonLevel r => r.Paratrons.OrderByDescending(x => x.Y).Select(x => (float?)(x.X + 5)).FirstOrDefault(),
            BattleStarLevel s => s.ReactorX + BattleStarLevel.ReactorWidth / 2f,
            _ => null,
        };

        if (level is MeteorLevel m)
        {
            // Slide away from the lowest meteor that is above the ship.
            var threat = m.Meteors.Where(x => x.Y > 90).OrderByDescending(x => x.Y).FirstOrDefault();
            float centre = shipX + 5;
            target = threat == null ? 120 : (threat.X + 5 < centre ? Math.Min(225, threat.X + 40) : Math.Max(10, threat.X - 30));
        }

        float aim = target ?? Canvas.Width / 2f;
        // A gentle weave makes the demo look alive when lined up.
        aim += MathF.Sin(world.Tick * 0.05f) * 6;
        _c.Pointer = new Vector2(aim, 100);
        float centreX = level is RadonLevel ? shipX + 5 : shipX + 5;
        _c.FirePressed = target.HasValue && Math.Abs(centreX - aim) < 10 && world.Tick % 6 == 0;
        _c.FireHeld = false;
    }
}
