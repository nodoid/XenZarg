using System;
using Microsoft.Xna.Framework;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;

namespace XenZarg.Core.Scenes;

/// <summary>
/// The start screen: the trilogy title over three small gameplay screenshots in a
/// triangle (Xenon 1 at the top, Zorgon's Revenge and The Genesis Probe below)
/// and a Play All Games option.
/// Pick one and press fire, or tap it, to jump straight in.
/// </summary>
public sealed class TitleScene : Scene
{
    private enum Choice
    {
        All,
        Xenon,
        Zorgon,
        Genesis,
    }

    private static readonly Rectangle XenonBox = new(84, 40, 72, 48);
    private static readonly Rectangle ZorgonBox = new(12, 104, 72, 48);
    private static readonly Rectangle GenesisBox = new(156, 104, 72, 48);
    private static readonly Rectangle AllBox = new(40, 168, 160, 22);

    private Choice _selected = Choice.All;

    public TitleScene(XenZargGame game) : base(game)
    {
    }

    protected override void Update()
    {
        var input = Game.Input;
        if (input.BackPressed)
        {
            Game.ShowMenu();
            return;
        }
        if (input.InstructionsPressed)
        {
            Game.ShowInstructions(new TitleScene(Game));
            return;
        }

        if (input.UpPressed)
            _selected = Choice.Xenon;
        if (input.LeftPressed)
            _selected = Choice.Zorgon;
        if (input.RightPressed)
            _selected = Choice.Genesis;
        if (input.DownPressed)
            _selected = Choice.All;

        foreach (char ch in input.Typed)
        {
            if (ch == '1')
                _selected = Choice.Xenon;
            else if (ch == '2')
                _selected = Choice.Zorgon;
            else if (ch == '3')
                _selected = Choice.Genesis;
            else if (ch == '4' || ch == 'A')
                _selected = Choice.All;
        }

        if (input.Pointer is Vector2 p)
        {
            var hover = HitTest(p);
            if (hover != null)
                _selected = hover.Value;
        }

        foreach (var tap in input.Taps)
        {
            var picked = HitTest(tap);
            if (picked == null)
                continue;
            _selected = picked.Value;
            Start(_selected);
            return;
        }

        if (input.FirePressed || input.ConfirmPressed)
            Start(_selected);
    }

    private static Choice? HitTest(Vector2 p)
    {
        if (AllBox.Contains(p))
            return Choice.All;
        if (XenonBox.Contains(p))
            return Choice.Xenon;
        if (ZorgonBox.Contains(p))
            return Choice.Zorgon;
        if (GenesisBox.Contains(p))
            return Choice.Genesis;
        return null;
    }

    private void Start(Choice choice)
    {
        switch (choice)
        {
            case Choice.All:
                Game.StartNewGameAt(Episode.Xenon, playAll: true);
                break;
            case Choice.Zorgon:
                Game.StartNewGameAt(Episode.Zorgon, playAll: false);
                break;
            case Choice.Genesis:
                Game.StartNewGameAt(Episode.Genesis, playAll: false);
                break;
            default:
                Game.StartNewGameAt(Episode.Xenon, playAll: false);
                break;
        }
    }

    public override void Draw(Canvas c)
    {
        const string Title = "THE XENON TRILOGY";
        c.TextScaled((Canvas.Width - Title.Length * OricFont.GlyphWidth * 2) / 2, 6, Title, OricColor.Yellow, 2);
        c.TextCentered(32, "XENON 1 / ZORGON / GENESIS", OricColor.Cyan);

        DrawXenonPreview(c, XenonBox, _selected == Choice.Xenon);
        DrawZorgonPreview(c, ZorgonBox, _selected == Choice.Zorgon);
        DrawGenesisPreview(c, GenesisBox, _selected == Choice.Genesis);

        BoxLabel(c, XenonBox, "XENON 1", _selected == Choice.Xenon);
        BoxLabel(c, ZorgonBox, "ZORGON", _selected == Choice.Zorgon);
        BoxLabel(c, GenesisBox, "GENESIS", _selected == Choice.Genesis);

        bool all = _selected == Choice.All;
        c.Fill(AllBox.X, AllBox.Y, AllBox.Width, AllBox.Height, all ? OricColor.Yellow : OricColor.Black);
        c.Fill(AllBox.X + 1, AllBox.Y + 1, AllBox.Width - 2, AllBox.Height - 2, all ? OricColor.Yellow : OricColor.Black);
        if (!all)
        {
            c.Fill(AllBox.X, AllBox.Y, AllBox.Width, 1, OricColor.Cyan);
            c.Fill(AllBox.X, AllBox.Y + AllBox.Height - 1, AllBox.Width, 1, OricColor.Cyan);
            c.Fill(AllBox.X, AllBox.Y, 1, AllBox.Height, OricColor.Cyan);
            c.Fill(AllBox.X + AllBox.Width - 1, AllBox.Y, 1, AllBox.Height, OricColor.Cyan);
        }
        c.TextCentered(170, "PLAY ALL GAMES", all ? OricColor.Blue : OricColor.Yellow);
        c.TextCentered(180, "FULL CAMPAIGN", all ? OricColor.Blue : OricColor.Cyan);

        if (Ticks / 20 % 2 == 0)
            c.TextCentered(192, Game.IsMobile ? "TAP A GAME OR PLAY ALL" : "SPACE: START", OricColor.Yellow);
        c.TextCentered(202, Game.IsMobile ? "BACK: OPTIONS" : "1 2 3: GAMES   4: ALL   ESC: OPTIONS", OricColor.White);
        c.TextCentered(210, XenZargGame.Credit + " - " + XenZargGame.BasedOn, OricColor.Cyan);
    }

    private static void BoxLabel(Canvas c, Rectangle box, string text, bool selected)
    {
        int y = box.Bottom + 2;
        int x = box.X + (box.Width - text.Length * OricFont.GlyphWidth) / 2;
        if (selected)
        {
            int w = text.Length * OricFont.GlyphWidth;
            c.Fill(x - 2, y - 1, w + 4, OricFont.GlyphHeight + 2, OricColor.Yellow);
            c.Text(x, y, text, OricColor.Blue);
        }
        else
        {
            c.Text(x, y, text, OricColor.Yellow);
        }
    }

    private static void Frame(Canvas c, Rectangle box, bool selected)
    {
        c.Fill(box.X, box.Y, box.Width, box.Height, OricColor.Black);
        var ink = selected ? OricColor.Yellow : OricColor.Cyan;
        int t = selected ? 2 : 1;
        c.Fill(box.X, box.Y, box.Width, t, ink);
        c.Fill(box.X, box.Y + box.Height - t, box.Width, t, ink);
        c.Fill(box.X, box.Y, t, box.Height, ink);
        c.Fill(box.X + box.Width - t, box.Y, t, box.Height, ink);
    }

    private static void Stars(Canvas c, Rectangle box)
    {
        for (int i = 0; i < 14; i++)
        {
            int x = box.X + 3 + (i * 17 + 5) % (box.Width - 6);
            int y = box.Y + 3 + (i * 11 + 7) % (box.Height - 6);
            c.Plot(x, y, i % 4 == 0 ? OricColor.White : OricColor.Cyan);
        }
    }

    private static void DrawXenonPreview(Canvas c, Rectangle box, bool selected)
    {
        Frame(c, box, selected);
        Stars(c, box);
        c.Draw(Sprite.Of(SpriteData.BirdAardUp), box.X + 10, box.Y + 8);
        c.Draw(Sprite.Of(SpriteData.BirdAardDown), box.X + 49, box.Y + 15);
        c.Draw(Sprite.Of(SpriteData.PlayerShip), box.X + 30, box.Y + 27);
        c.Fill(box.X + 28, box.Y + 44, 16, 1, OricColor.Yellow);
    }

    private static void DrawZorgonPreview(Canvas c, Rectangle box, bool selected)
    {
        Frame(c, box, selected);
        Stars(c, box);
        c.Fill(box.X + 2, box.Y + 38, box.Width - 4, 3, OricColor.Yellow);
        c.Fill(box.X + 2, box.Y + 41, box.Width - 4, 1, OricColor.Red);
        c.Fill(box.X + 8, box.Y + 34, 18, 4, OricColor.Green);
        c.Draw(Sprite.Of(SpriteData.ZHeroWalkRightF0), box.X + 8, box.Y + 20);
        c.Draw(Sprite.Of(SpriteData.ZSpiderF1), box.X + 42, box.Y + 24);
        c.Draw(Sprite.Of(SpriteData.ZStone), box.X + 54, box.Y + 33);
    }

    private static void DrawGenesisPreview(Canvas c, Rectangle box, bool selected)
    {
        Frame(c, box, selected);
        Stars(c, box);
        c.Draw(Sprite.Of(SpriteData.GPlanet), box.X + 8, box.Y + 7);
        c.Draw(Sprite.Of(SpriteData.GSpecBird), box.X + 50, box.Y + 13);
        c.Draw(Sprite.Of(SpriteData.GShip), box.X + 30, box.Y + 30);
        c.Fill(box.X + 2, box.Y + 43, box.Width - 4, 2, OricColor.Yellow);
    }
}
