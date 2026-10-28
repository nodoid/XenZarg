using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Session;

namespace XenZarg.Core.Scenes;

/// <summary>The "HALL OF FAME", with arcade style name entry after a qualifying game.</summary>
public sealed class HallOfFameScene : Scene
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789. <";
    private const char EndMark = '<';
    private readonly GameSession _finished;
    private bool _entering;
    private char[] _name;
    private int _cursor;
    private int _letter;
    private int _rank = -1;
    private int _repeat;

    public HallOfFameScene(XenZargGame game, GameSession finished) : base(game)
    {
        _finished = finished;
    }

    public override void Enter()
    {
        if (_finished != null && Game.HighScores.Qualifies(_finished.Score))
        {
            _entering = true;
            _name = "     ".ToCharArray();
            Game.Input.CalibrateTilt();
        }
    }

    protected override void Update()
    {
        var input = Game.Input;
        if (_entering)
        {
            UpdateEntry();
            return;
        }
        if (input.BackPressed)
            Game.ShowMenu();
        else if (Ticks > 25 && (input.ConfirmPressed || input.FirePressed))
            Game.StartNewGame();
        else if (Ticks > 600)
            Game.ShowTitle();
    }

    private void UpdateEntry()
    {
        var input = Game.Input;
        if (Ticks < 40)
            return;

        foreach (char ch in input.Typed)
        {
            if (ch == '\b')
            {
                if (_cursor > 0)
                    _name[--_cursor] = ' ';
            }
            else if (_cursor < HighScoreTable.NameLength)
            {
                _name[_cursor++] = ch;
            }
        }

        // Z / X, cursor keys or tilt cycle the letter under the cursor.
        int step = 0;
        if (input.LeftPressed || input.UpPressed) step = -1;
        if (input.RightPressed || input.DownPressed) step = 1;
        if (step != 0)
            _repeat = 1;
        else if (Math.Abs(input.MoveX) > 0.5f)
        {
            if (++_repeat % 15 == 0)
                step = Math.Sign(input.MoveX);
        }
        else
            _repeat = 0;
        if (step != 0)
        {
            _letter = (_letter + step + Alphabet.Length) % Alphabet.Length;
            Game.Sound.Play(Sfx.Step);
        }

        bool accept = (input.FirePressed || input.ConfirmPressed) && input.Typed.Count == 0;
        if (accept && _cursor < HighScoreTable.NameLength)
        {
            char ch = Alphabet[_letter];
            if (ch == EndMark)
            {
                Finish();
                return;
            }
            _name[_cursor++] = ch;
            Game.Sound.Play(Sfx.Shoot);
        }
        if (_cursor >= HighScoreTable.NameLength || input.BackPressed)
            Finish();
    }

    private void Finish()
    {
        _entering = false;
        _rank = Game.HighScores.Add(_finished.Score, new string(_name));
        Game.HighScores.Save();
        Game.Sound.Play(Sfx.Bonus);
    }

    public override void Draw(Canvas c)
    {
        c.TextCentered(0, "THE XENON HALL OF FAME", OricColor.Green);
        if (_entering)
        {
            c.TextCentered(24, "GAME OVER", OricColor.Red);
            c.TextCentered(48, "YOUR SCORE " + _finished.Score.ToString("D6"), OricColor.Yellow);
            c.TextCentered(72, "ENTER YOUR NAME", OricColor.Cyan);
            int x0 = (Canvas.Width - HighScoreTable.NameLength * 12) / 2;
            for (int i = 0; i < HighScoreTable.NameLength; i++)
            {
                char ch = i < _cursor ? _name[i] : (i == _cursor ? Alphabet[_letter] : '-');
                bool blink = i == _cursor && Ticks / 10 % 2 == 0;
                if (blink)
                    c.Fill(x0 + i * 12 - 1, 95, 8, 10, OricColor.Yellow);
                c.Text(x0 + i * 12, 96, ch == EndMark ? "<" : ch.ToString(), blink ? OricColor.Blue : OricColor.White);
            }
            c.TextCentered(124, Game.IsMobile ? "TILT TO CHANGE LETTER" : "Z / X OR TYPE TO CHANGE", OricColor.Yellow);
            c.TextCentered(136, Game.IsMobile ? "TAP TO ACCEPT" : "SPACE TO ACCEPT", OricColor.Yellow);
            c.TextCentered(148, "< ENDS THE NAME", OricColor.Yellow);
            return;
        }

        if (_finished != null)
            c.TextCentered(8, "GAME OVER", OricColor.Red);
        c.TextAt(6, 16, "SCORE", OricColor.Cyan);
        c.TextAt(26, 16, "NAME", OricColor.Cyan);
        var entries = Game.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var ink = i == _rank && Ticks / 8 % 2 == 0 ? OricColor.White : OricColor.Yellow;
            c.TextAt(6, 32 + i * 16, entries[i].Score.ToString("D6"), ink);
            c.TextAt(18, 32 + i * 16, entries[i].Name, ink);
        }
        c.TextCentered(192, Game.IsMobile ? "TAP TO PLAY : BACK TO GOTO MENU" : "PRESS SPACE TO PLAY : ESC TO GOTO MENU", OricColor.Cyan);
        c.TextCentered(212, XenZargGame.Credit + " - " + XenZargGame.BasedOn, OricColor.White);
    }
}
