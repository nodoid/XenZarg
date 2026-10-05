using System;
using System.Collections.Generic;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Scenes;

/// <summary>
/// The Xenon 1 title: the Xenon cruiser beams down and dissolves the logo, as on the Oric,
/// with the PFJ credit in place of the original author line.
/// </summary>
public sealed class TitleScene : Scene
{
    private const int ShipX = 66, ShipY = 11;
    private const int LogoX = 54, LogoY = 134;
    private const int Cycle = 420;
    private readonly Sprite _ship = Sprite.Of(SpriteData.TitleShip);
    private readonly Sprite _logo = Sprite.Of(SpriteData.TitleLogo);
    private readonly List<(int x, int y, OricColor c)> _logoPixels = new();
    private readonly Random _rng = new(1983);

    public TitleScene(XenZargGame game) : base(game)
    {
        for (int y = 0; y < _logo.Height; y++)
            for (int x = 0; x < _logo.Width; x++)
                if (_logo.ColorAt(x, y) is OricColor c)
                    _logoPixels.Add((x, y, c));
        // Shuffle once: the order pixels vanish in.
        for (int i = _logoPixels.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_logoPixels[i], _logoPixels[j]) = (_logoPixels[j], _logoPixels[i]);
        }
    }

    protected override void Update()
    {
        var input = Game.Input;
        if (input.ConfirmPressed || input.FirePressed)
            Game.ShowMenu();
        else if (input.InstructionsPressed)
            Game.ShowInstructions(new TitleScene(Game));
        else if (input.BackPressed)
            Game.Quit();
        else if (Ticks > Cycle * 2)
            Game.SwitchTo(new HallOfFameScene(Game, null));
    }

    public override void Draw(Canvas c)
    {
        int t = Ticks % Cycle;
        c.Draw(_ship, ShipX, ShipY);

        // Beam: 100..140 grows, 140..260 holds, 260..300 retracts.
        int beamTop = ShipY + _ship.Height;
        int beamFull = LogoY - beamTop;
        int beamLen = t switch
        {
            < 100 => 0,
            < 140 => (t - 100) * beamFull / 40,
            < 260 => beamFull,
            < 300 => (300 - t) * beamFull / 40,
            _ => 0,
        };
        if (beamLen > 0)
            c.Fill(119 - 1, beamTop, 2, beamLen, OricColor.White);

        // Dissolve: 140..220 vanishes, 300..380 rematerialises.
        double gone = t switch
        {
            < 140 => 0,
            < 220 => (t - 140) / 80.0,
            < 300 => 1,
            < 380 => 1 - (t - 300) / 80.0,
            _ => 0,
        };
        int hidden = (int)(gone * _logoPixels.Count);
        for (int i = hidden; i < _logoPixels.Count; i++)
        {
            var p = _logoPixels[i];
            c.Plot(LogoX + p.x, LogoY + p.y, p.c);
        }
        if (gone > 0 && gone < 1)
        {
            // Sparkle of the disintegrating letters.
            for (int i = 0; i < 60; i++)
            {
                var p = _logoPixels[_rng.Next(Math.Max(1, hidden))];
                c.Plot(LogoX + p.x + _rng.Next(-2, 3), LogoY + p.y + _rng.Next(-2, 3), OricColor.Red);
            }
        }

        c.TextCentered(184, XenZargGame.Credit, OricColor.White);
        c.TextCentered(196, XenZargGame.BasedOn, OricColor.Cyan);
        if (Ticks / 25 % 2 == 0)
            c.TextCentered(212, Game.IsMobile ? "TAP TO START" : "PRESS SPACE TO START", OricColor.Yellow);
    }
}
