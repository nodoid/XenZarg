using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;

namespace XenZarg.Core.Zorgon;

/// <summary>
/// The Zorgon's Revenge title: the logo, our hero walking in, Princess Roz and the scrolling
/// credits (now crediting PFJ and the PSS originals), followed by the instructions page.
/// </summary>
public sealed class ZorgonIntroScene : Scene
{
    public const int IntroTicks = 700;
    private const int HeroFeet = 161;
    private readonly GameSession _session;
    private readonly Hero _hero = new(210, HeroFeet) { FacingLeft = true, MinX = -40 };
    private bool _instructions;
    private int _instructionTicks;

    private static readonly (string text, OricColor ink)[] Credits =
    [
        ("XENON 1 COMPLETED !", OricColor.Cyan),
        ("", OricColor.Black),
        ("ZORGONS REVENGE", OricColor.Green),
        ("", OricColor.Black),
        (XenZargGame.Credit, OricColor.Magenta),
        ("", OricColor.Black),
        ("Based on the", OricColor.White),
        ("PSS originals", OricColor.White),
        ("", OricColor.Black),
        ("Rescue", OricColor.Yellow),
        ("Princess Roz", OricColor.Yellow),
    ];

    public ZorgonIntroScene(XenZargGame game, GameSession session) : base(game)
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
            if (++_instructionTicks > 15 && (input.ConfirmPressed || input.FirePressed))
                Game.StartZorgon();
            return;
        }
        if (_hero.X > 18)
            _hero.X -= 1f;
        if ((Ticks > 30 && (input.ConfirmPressed || input.FirePressed)) || Ticks > IntroTicks)
            _instructions = true;
    }

    public override void Draw(Canvas c)
    {
        if (_instructions)
        {
            foreach (var line in InstructionPages.Zorgon())
                c.TextAt(line.Column, line.Y, line.Text, line.Ink);
            c.TextCentered(216, Game.IsMobile ? "TAP TO CONTINUE" : "PRESS SPACE TO CONTINUE", OricColor.White);
            return;
        }

        c.Draw(SpriteData.ZTitleLogoZORGONSREVENGE, 42, 0);
        // The credit line cycles through the Oric colours as the original author line did.
        c.TextCentered(80, XenZargGame.Credit, (OricColor)(1 + Ticks / 3 % 7));

        c.TextAt(1, 120, "Starring", OricColor.Cyan);
        c.TextAt(32, 120, "Starring", OricColor.Cyan);
        _hero.Draw(c);
        c.TextAt(1, 173, "Our Hero", OricColor.Red);
        if (_hero.X <= 18)
        {
            c.Draw(SpriteData.ZPrincessRoz, 203, 139);
            c.TextAt(32, 173, "Princess", OricColor.Red);
            c.TextAt(34, 185, "Roz", OricColor.Red);
        }

        // Credits scroll up between the two figures.
        int baseY = 190 - (Ticks - 200) / 3;
        for (int i = 0; i < Credits.Length; i++)
        {
            int y = baseY + i * 10;
            if (y >= 112 && y <= 186 && Credits[i].text.Length > 0)
                c.TextCentered(y, Credits[i].text, Credits[i].ink);
        }
    }
}
