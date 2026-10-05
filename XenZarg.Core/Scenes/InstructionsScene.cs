using System.Collections.Generic;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Scenes;

/// <summary>A line of an instruction page: text at a character column and pixel row.</summary>
public readonly record struct PageLine(int Column, int Y, string Text, OricColor Ink);

/// <summary>Instruction pages for both games, laid out like the Oric originals.</summary>
public sealed class InstructionsScene : Scene
{
    private readonly Scene _returnTo;
    private readonly List<PageLine[]> _pages;
    private int _page;

    public InstructionsScene(XenZargGame game, Scene returnTo) : base(game)
    {
        _returnTo = returnTo;
        _pages =
        [
            InstructionPages.Xenon(),
            InstructionPages.Zorgon(),
            InstructionPages.Controls(game.IsMobile),
        ];
    }

    public int PageCount => _pages.Count;

    protected override void Update()
    {
        var input = Game.Input;
        if (input.BackPressed)
        {
            Game.SwitchTo(_returnTo ?? new MenuScene(Game));
            return;
        }
        if (Ticks > 10 && (input.ConfirmPressed || input.FirePressed))
        {
            // The scene switch happens on the next update, so stay on the last page until then.
            if (_page + 1 < _pages.Count)
                _page++;
            else
                Game.SwitchTo(_returnTo ?? new MenuScene(Game));
        }
    }

    public override void Draw(Canvas c)
    {
        foreach (var line in _pages[_page])
            c.TextAt(line.Column, line.Y, line.Text, line.Ink);
        string prompt = Game.IsMobile ? "TAP TO CONTINUE" : "PRESS SPACE TO CONTINUE";
        c.TextCentered(216, prompt, OricColor.White);
    }
}

public static class InstructionPages
{
    private const OricColor H = OricColor.Red;
    private const OricColor T = OricColor.Yellow;

    /// <summary>The Xenon 1 instruction page, verbatim from the Oric original.</summary>
    public static PageLine[] Xenon() =>
    [
        new(15, 0, "INSTRUCTIONS", OricColor.White),
        new(2, 8, "LEVEL1", H),
        new(3, 16, "DESTROY THE BIRD-AARDS WORTH 150 Pts", T),
        new(2, 24, "LEVEL2", H),
        new(2, 32, "DESTROY THE LARGE AARDS.WHEN HIT THE", T),
        new(2, 40, "AARDS WILL CLONE,ONE DIVING TOWARDS", T),
        new(2, 48, "YOU EXPLODING ON IMPACT.THE RADIATION", T),
        new(2, 56, "CAUSED BY THIS IS FATAL-KEEP AWAY !", T),
        new(2, 64, "LEVEL3", H),
        new(2, 72, "AVOID THE METEORITES", T),
        new(2, 80, "LEVEL4", H),
        new(2, 88, "HAVING REACHED THE PLANET RADON.YOUR", T),
        new(2, 96, "OBJECTIVE IS TO KILL 10-30 PARATRONS", T),
        new(2, 104, "(DEPENDING ON WHICH SKILL LEVEL YOU", T),
        new(2, 112, "HAVE CHOSEN).", T),
        new(2, 120, "LEVEL5", H),
        new(2, 128, "TO VAPOURISE THE ZORGON BATTLE STAR", T),
        new(2, 136, "YOU MUST HIT ITS MOVING NUCLEAR", T),
        new(2, 144, "REACTOR.THE FASTER YOU ACHIEVE THIS", T),
        new(2, 152, "THE HIGHER THE BONUS YOU RECEIVE.", T),
        new(2, 160, "REFUELLING", H),
        new(2, 168, "YOU MAY REFUEL AT ANY STAGE OF THE", T),
        new(2, 176, "GAME,IF YOUR FUEL IS IN THE YELLOW OR", T),
        new(2, 184, "RED REGION,BY PRESSING THE DISENGAGE", T),
        new(2, 192, "BUTTON AND DOCKING WITH THE", T),
        new(2, 200, "MOTHERSHIP'S FLASHING SERVICE BAY", T),
    ];

    /// <summary>Zorgon's Revenge story and missions.</summary>
    public static PageLine[] Zorgon() =>
    [
        new(12, 0, "ZORGON'S REVENGE", OricColor.White),
        new(2, 8, "THE ZORGONS HAVE CAPTURED THE XENON", T),
        new(2, 16, "PRINCESS ROZ.BEHIND EACH DOOR LIES A", T),
        new(2, 24, "MISSION TO WIN A MAGIC STONE.EACH", T),
        new(2, 32, "STONE BRIDGES PART OF THE CHASM", T),
        new(2, 40, "AROUND ZORGON'S CASTLE.", T),
        new(2, 48, "SPIDER", H),
        new(2, 56, "CATCH THE ROD,RIDE THE LIFT,JUMP TO", T),
        new(2, 64, "HIT THE BUTTON,THEN RETURN HOME.", T),
        new(2, 72, "QUADNOGS", H),
        new(2, 80, "JUMP INTO A WHITE SHOT TO KILL A", T),
        new(2, 88, "QUADNOG.RED SHOTS AND BEAMS KILL.", T),
        new(2, 96, "FIND THE SWITCH,THEN GO TO THE TOP.", T),
        new(2, 104, "SHIP", H),
        new(2, 112, "SHOOT TEN ALIENS,THEN THE MOTHERSHIP", T),
        new(2, 120, "BIRD", H),
        new(2, 128, "JUMP ONTO THE BIRD AND RIDE IT UP", T),
        new(2, 136, "TO THE LEDGES.WALK TO THE STONE.", T),
        new(2, 144, "BRIDGE", H),
        new(2, 152, "JUMP TO HIT THE CRANE'S BUTTON.", T),
        new(2, 160, "CASTLE", H),
        new(2, 168, "JUMP UNDER THE ARMED LEDGE TO FIRE", T),
        new(2, 176, "ITS CANNON.THREE HITS FROM THE LEFT", T),
        new(2, 184, "CANNON TOPPLE ZORGON.", T),
        new(2, 200, "WALKING OFF ANY EDGE IS FATAL !", OricColor.Cyan),
    ];

    /// <summary>Controls for the platform the game is running on.</summary>
    public static PageLine[] Controls(bool mobile)
    {
        var lines = new List<PageLine> { new(16, 0, "CONTROLS", OricColor.White) };
        int y = 16;
        void Head(string s) { lines.Add(new(2, y, s, H)); y += 8; }
        void Line(string s) { lines.Add(new(2, y, s, T)); y += 8; }
        if (mobile)
        {
            Head("STEERING");
            Line("TILT THE DEVICE LEFT OR RIGHT TO");
            Line("MOVE.TILT IT TOWARDS OR AWAY FROM");
            Line("YOU TO MOVE DOWN OR UP WHERE A");
            Line("SCREEN ALLOWS IT.THE ANGLE YOU ARE");
            Line("HOLDING WHEN A LEVEL STARTS IS LEVEL.");
            y += 8;
            Head("FIRE / JUMP");
            Line("TAP ANYWHERE ON THE SCREEN.");
            y += 8;
            Head("DISENGAGE (REFUEL)");
            Line("TAP THE FUEL GAUGE AT THE TOP.");
            y += 8;
            Head("PAUSE / MENU");
            Line("TAP WITH THREE FINGERS TO PAUSE.");
            Line("USE BACK TO RETURN TO THE MENU.");
        }
        else
        {
            Head("KEYBOARD");
            Line("Z         LEFT  (UP ON VERTICAL");
            Line("X         RIGHT (DOWN    SCREENS)");
            Line("SPACE     FIRE / JUMP");
            Line("CURSORS   ALSO MOVE AS ON THE ORIC");
            Line("CURSOR UP DISENGAGE (REFUEL)");
            Line("P         PAUSE");
            Line("ESC       RETURN TO MENU");
            Line("F11       FULL SCREEN");
            y += 8;
            Head("MOUSE");
            Line("MOVE THE MOUSE TO STEER.");
            Line("LEFT BUTTON FIRES / JUMPS.");
            Line("RIGHT BUTTON OR CLICKING THE FUEL");
            Line("GAUGE DISENGAGES TO REFUEL.");
        }
        y += 16;
        lines.Add(new(2, y, "XENON 1 THEN ZORGON'S REVENGE - ONE", OricColor.Cyan));
        lines.Add(new(2, y + 8, "CONTINUOUS GAME. " + XenZargGame.Credit + ",", OricColor.Cyan));
        lines.Add(new(2, y + 16, XenZargGame.BasedOn + ".", OricColor.Cyan));
        return lines.ToArray();
    }
}
