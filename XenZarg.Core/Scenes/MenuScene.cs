using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;

namespace XenZarg.Core.Scenes;

/// <summary>
/// The key / function page of the original, extended into a menu that works with
/// keys (Z / X / SPACE), the mouse or a tap.
/// </summary>
public sealed class MenuScene : Scene
{
    private const int TableTop = 30;
    private const int MenuTop = 146;
    private const int ItemHeight = 10;
    private const int Items = 5;
    private int _selected;

    private static readonly (string key, string function)[] DesktopKeys =
    [
        ("Z / CURSOR LEFT", "LEFT"),
        ("X / CURSOR RIGHT", "RIGHT"),
        ("SPACE / CLICK", "FIRE / JUMP"),
        ("MOUSE", "STEER"),
        ("CURSOR UP", "DISENGAGE"),
        ("ESC", "RETURN TO MENU"),
        ("P", "PAUSE"),
        ("V", "ALTER VOLUME"),
        ("I", "INSTRUCTIONS"),
        ("S", "SKILL LEVEL"),
    ];

    private static readonly (string key, string function)[] MobileKeys =
    [
        ("TILT LEFT/RIGHT", "STEER"),
        ("TILT FWD / BACK", "UP / DOWN"),
        ("TAP", "FIRE / JUMP"),
        ("TAP FUEL GAUGE", "DISENGAGE"),
        ("THREE FINGER TAP", "PAUSE"),
        ("BACK", "RETURN TO MENU"),
    ];

    public MenuScene(XenZargGame game) : base(game)
    {
    }

    private string ItemText(int i) => i switch
    {
        0 => Game.IsMobile ? "TAP HERE TO PLAY" : "PRESS THE SPACE BAR TO PLAY",
        1 => "INSTRUCTIONS",
        2 => $"SKILL LEVEL {Game.Settings.Skill}",
        3 => "VOLUME " + VolumeName(),
        _ => "HALL OF FAME",
    };

    private string VolumeName() => Game.Sound.Volume switch
    {
        <= 0f => "OFF",
        < 0.5f => "LOW",
        < 0.9f => "MEDIUM",
        _ => "HIGH",
    };

    protected override void Update()
    {
        var input = Game.Input;
        if (input.SkillPressed)
            Activate(2);
        if (input.VolumePressed)
            Activate(3);
        if (input.InstructionsPressed)
            Activate(1);
        if (input.BackPressed)
        {
            Game.ShowTitle();
            return;
        }
        if (input.UpPressed || input.LeftPressed)
            _selected = (_selected + Items - 1) % Items;
        if (input.DownPressed || input.RightPressed)
            _selected = (_selected + 1) % Items;

        foreach (var tap in input.Taps)
        {
            int row = (int)((tap.Y - MenuTop + 2) / ItemHeight);
            if (tap.Y >= MenuTop - 2 && row >= 0 && row < Items)
            {
                _selected = row;
                Activate(row);
                return;
            }
        }
        if (input.Pointer is { } p)
        {
            int row = (int)((p.Y - MenuTop + 2) / ItemHeight);
            if (p.Y >= MenuTop - 2 && row >= 0 && row < Items)
                _selected = row;
        }
        if (input.Taps.Count == 0 && (input.FirePressed || input.ConfirmPressed))
            Activate(_selected);
    }

    private void Activate(int item)
    {
        switch (item)
        {
            case 0:
                Game.StartNewGame();
                break;
            case 1:
                Game.ShowInstructions(new MenuScene(Game));
                break;
            case 2:
                Game.Settings.Skill = Game.Settings.Skill % GameSession.MaxSkill + 1;
                Game.Settings.Save();
                Game.Sound.Play(Sfx.Shoot);
                break;
            case 3:
                Game.Sound.CycleVolume();
                Game.Settings.Volume = (Game.Settings.Volume + 1) % 4;
                Game.Settings.Save();
                break;
            default:
                Game.ShowHallOfFame();
                break;
        }
    }

    public override void Draw(Canvas c)
    {
        c.TextCentered(0, "XENON 1 & ZORGON'S REVENGE", OricColor.Green);
        bool mobile = Game.IsMobile;
        c.TextAt(3, 16, mobile ? "CONTROL" : "KEY", OricColor.Cyan);
        c.TextAt(22, 16, "FUNCTION", OricColor.Cyan);
        var keys = mobile ? MobileKeys : DesktopKeys;
        for (int i = 0; i < keys.Length; i++)
        {
            c.TextAt(3, TableTop + i * 11, keys[i].key, OricColor.Yellow);
            c.TextAt(22, TableTop + i * 11, keys[i].function, OricColor.Yellow);
        }

        for (int i = 0; i < Items; i++)
        {
            string text = ItemText(i);
            int y = MenuTop + i * ItemHeight;
            if (i == _selected)
            {
                // Oric inverse video highlight.
                int w = (text.Length + 2) * 6;
                c.Fill((Canvas.Width - w) / 2, y - 1, w, 9, OricColor.Yellow);
                c.TextCentered(y, text, OricColor.Blue);
            }
            else
            {
                c.TextCentered(y, text, i == 0 ? OricColor.Yellow : OricColor.White);
            }
        }

        c.TextCentered(200, XenZargGame.Credit, OricColor.Cyan);
        c.TextCentered(210, XenZargGame.BasedOn, OricColor.Red);
    }
}
