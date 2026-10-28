using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;

namespace XenZarg.Core.Genesis;

/// <summary>
/// The Genesis Probe title: IJK's logo line, the game's name, the radiation warning and
/// the story of Radon, followed by the mission instructions.
/// </summary>
public sealed class GenesisIntroScene : Scene
{
    private readonly GameSession _session;
    private bool _instructions;

    public GenesisIntroScene(XenZargGame game, GameSession session) : base(game)
    {
        _session = session;
    }

    public override void Enter() => Game.Sound.Play(Sfx.Victory);

    protected override void Update()
    {
        var input = Game.Input;
        if (input.BackPressed)
        {
            Game.GameOver();
            return;
        }
        if (_instructions)
        {
            if (Ticks > 20 && (input.ConfirmPressed || input.FirePressed))
                Game.StartGenesis();
            return;
        }
        if (Ticks > 25 && (input.ConfirmPressed || input.FirePressed))
            _instructions = true;
    }

    public override void Draw(Canvas c)
    {
        if (_instructions)
        {
            foreach (var line in InstructionPages.Genesis())
                c.TextAt(line.Column, line.Y, line.Text, line.Ink);
            c.TextCentered(216, Game.IsMobile ? "TAP TO BEGIN" : "PRESS SPACE TO BEGIN", OricColor.White);
            return;
        }

        c.Fill(0, 0, Canvas.Width, Canvas.Height, OricColor.Black);
        for (int i = 0; i < 40; i++)
        {
            int x = (i * 61 + 19) % Canvas.Width;
            int y = (i * 47 + 13) % 200;
            c.Plot(x, y, i % 5 == 0 && Ticks / 10 % 2 == 0 ? OricColor.White : OricColor.Cyan);
        }

        c.TextCentered(4, "I.J.K. SOFTWARE LTD PRESENT", OricColor.Cyan);
        c.TextScaled(102, 18, "THE", OricColor.Green, 2);
        c.TextScaled(42, 38, "GENESIS PROBE", OricColor.Green, 2);
        c.TextCentered(64, "WRITTEN BY WUN", OricColor.White);
        c.TextCentered(72, "COPYRIGHT 1985", OricColor.White);

        c.Fill(20, 84, 200, 52, OricColor.Magenta);
        c.Fill(22, 86, 196, 48, OricColor.Black);
        c.TextCentered(90, "RADON IS DYING. THE ZORGONS", OricColor.Yellow);
        c.TextCentered(98, "ARE WAITING BEYOND THE STARS.", OricColor.Yellow);
        c.TextCentered(110, "COLLECT TWO OF EVERY SPECIES", OricColor.White);
        c.TextCentered(118, "FROM FIVE PLANETS, THEN RETURN", OricColor.White);
        c.TextCentered(126, "TO RADON WITH THE GENESIS PROBE.", OricColor.White);

        c.TextAt(6, 146, "LEVEL " + _session.Skill, OricColor.Cyan);
        c.TextAt(26, 146, "VOLUME " + VolumeName(), OricColor.Cyan);
        c.TextCentered(162, "RADON NEEDS YOU", OricColor.Magenta);
        if (Ticks / 20 % 2 == 0)
            c.TextCentered(176, Game.IsMobile ? "TAP TO START" : "START: PRESS FIRE", OricColor.Yellow);
        c.TextCentered(196, XenZargGame.Credit, OricColor.White);
        c.TextCentered(206, "BASED ON THE IJK ORIGINALS", OricColor.Cyan);
    }

    private string VolumeName() => Game.Sound.Volume switch
    {
        <= 0f => "OFF",
        < 0.5f => "LOW",
        < 0.9f => "MEDIUM",
        _ => "HIGH",
    };
}
