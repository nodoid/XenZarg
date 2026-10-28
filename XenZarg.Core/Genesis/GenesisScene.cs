using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Genesis.Missions;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;

namespace XenZarg.Core.Genesis;

/// <summary>
/// Runs Xenon 3 - The Genesis Probe: pick a planet, fly the hyperspace run,
/// then play its mission. Clear the five outer planets to open Radon.
/// </summary>
public sealed class GenesisScene : Scene, IPausable
{
    public enum State { Select, Hyper, Playing, Message }

    private readonly GameSession _session;
    private readonly GenesisWorld _world;
    private readonly GenesisCampaign _campaign = new();
    private int _selected;
    private Planet _planet;
    private Hyperdrive _hyper;
    private GenesisMission _mission;
    private State _state = State.Select;
    private string _message;
    private int _messageTicks;

    /// <summary>Ticks "THE GENESIS PROBE IS SAFE" is shown before the high score table.</summary>
    private const int FinalMessageTicks = 5 * XenZargGame.TicksPerSecond;

    private static readonly (int x, int y)[] IconPos =
        [(54, 64), (112, 64), (170, 64), (54, 112), (112, 112), (170, 112)];

    public GenesisScene(XenZargGame game, GameSession session) : base(game)
    {
        _session = session;
        _world = new GenesisWorld(session, game.Input, game.Sound, new Random());
    }

    public State Current => _state;
    public GenesisMission Mission => _mission;
    public GenesisCampaign Campaign => _campaign;
    public GenesisWorld World => _world;

    public static GenesisMission CreateMission(Planet planet, GenesisWorld world) =>
        GenesisPlanets.Kind(planet) switch
        {
            MissionKind.Capture => new CaptureMission(world, planet),
            MissionKind.Worms => new WormMission(world),
            _ => new RadonBattle(world),
        };

    protected override void Update()
    {
        if (Game.Input.BackPressed)
        {
            Game.GameOver();
            return;
        }
        _world.Tick++;
        if (_messageTicks > 0)
            _messageTicks--;

        switch (_state)
        {
            case State.Select:
                UpdateSelect();
                break;
            case State.Hyper:
                UpdateHyper();
                break;
            case State.Playing:
                UpdatePlaying();
                break;
            case State.Message:
                if (_messageTicks <= 0)
                    FinishMessage();
                break;
        }
    }

    private void UpdateSelect()
    {
        var input = Game.Input;
        if (input.LeftPressed)
            _selected = Wrap(_selected, -1);
        if (input.RightPressed)
            _selected = Wrap(_selected, 1);
        if (input.UpPressed)
            _selected = Wrap(_selected, -3);
        if (input.DownPressed)
            _selected = Wrap(_selected, 3);

        if (input.Pointer is Microsoft.Xna.Framework.Vector2 hover)
        {
            for (int i = 0; i < IconPos.Length; i++)
            {
                var (x, y) = IconPos[i];
                if (hover.X >= x - 6 && hover.X <= x + 22 && hover.Y >= y - 6 && hover.Y <= y + 26)
                    _selected = i;
            }
        }

        int chosen = -1;
        foreach (char ch in input.Typed)
            if (ch >= '1' && ch <= '6')
                chosen = ch - '1';
        foreach (var tap in input.Taps)
        {
            for (int i = 0; i < IconPos.Length; i++)
            {
                var (x, y) = IconPos[i];
                if (tap.X >= x - 6 && tap.X <= x + 22 && tap.Y >= y - 6 && tap.Y <= y + 26)
                    chosen = i;
            }
        }
        if (chosen < 0 && input.Taps.Count == 0 && (input.FirePressed || input.ConfirmPressed))
            chosen = _selected;
        if (chosen < 0)
            return;

        var planet = (Planet)chosen;
        if (_campaign.IsDone(planet))
        {
            _message = "PLANET ALREADY CLEARED";
            _messageTicks = 70;
            Game.Sound.Play(Sfx.Warning);
            return;
        }
        if (!_campaign.CanChoose(planet))
        {
            _message = "RADON OPENS AFTER FIVE PLANETS";
            _messageTicks = 70;
            Game.Sound.Play(Sfx.Warning);
            return;
        }

        _selected = chosen;
        _planet = planet;
        _hyper = new Hyperdrive(_world.Rng, _world.Sound);
        _state = State.Hyper;
        _message = null;
        Game.Input.CalibrateTilt();
        Game.Sound.Play(Sfx.Start);
    }

    private void UpdateHyper()
    {
        if (_hyper.Completed)
        {
            BeginMission();
            return;
        }
        _hyper.Update(Game.Input);
        if (!_hyper.Failed)
            return;
        _hyper.Reset();
        _message = "HYPERDRIVE STALLED - TRY AGAIN";
        _messageTicks = 90;
        Game.Sound.Play(Sfx.Warning);
    }

    private void BeginMission()
    {
        _mission = CreateMission(_planet, _world);
        _state = State.Playing;
        _hyper = null;
        Game.Input.CalibrateTilt();
    }

    private void UpdatePlaying()
    {
        _mission.Update();
        if (_mission.Completed)
        {
            _session.AddScore(_mission.CompletionBonus);
            _campaign.Complete(_planet);
            bool safe = _planet == Planet.Radon;
            _message = safe
                ? "THE GENESIS PROBE IS SAFE"
                : "PLANET " + _mission.Name + " CLEARED  BONUS " + _mission.CompletionBonus;
            // The final message is held for five seconds before the high score table.
            _messageTicks = safe ? FinalMessageTicks : 130;
            _state = State.Message;
            Game.Sound.Play(Sfx.Bonus);
        }
        else if (_mission.Failed)
        {
            if (_session.IsGameOver)
            {
                Game.GameOver();
                return;
            }
            _message = "SHIELD LOST";
            _messageTicks = 100;
            _state = State.Message;
            Game.Sound.Play(Sfx.PlayerDeath);
        }
    }

    private void FinishMessage()
    {
        if (_planet == Planet.Radon && _campaign.IsDone(Planet.Radon))
        {
            Game.GenesisCompleted();
            return;
        }
        _mission = null;
        _hyper = null;
        _message = null;
        _state = State.Select;
        _selected = (int)_campaign.NextOpen(_planet);
        Game.Sound.Play(Sfx.Warning);
    }

    private int Wrap(int index, int step) => ((index + step) % GenesisPlanets.PlanetCount + GenesisPlanets.PlanetCount) % GenesisPlanets.PlanetCount;

    public override void Draw(Canvas c)
    {
        switch (_state)
        {
            case State.Select:
                DrawSelect(c);
                break;
            case State.Hyper:
                _hyper.Draw(c);
                _world.DrawHud(c, 0);
                break;
            case State.Playing:
                _mission.Draw(c);
                _world.DrawHud(c, _mission.Specimens);
                break;
            case State.Message:
                if (_mission != null)
                {
                    _mission.Draw(c);
                    _world.DrawHud(c, _mission.Specimens);
                }
                else
                {
                    SelectBackdrop(c);
                    _world.DrawHud(c, 0);
                }
                c.Fill(10, 92, Canvas.Width - 20, 16, OricColor.Black);
                c.TextCentered(94, _message ?? "", OricColor.Yellow);
                break;
        }

        if (_messageTicks > 0 && _state != State.Message && _message != null)
        {
            c.Fill(20, 92, Canvas.Width - 40, 16, OricColor.Black);
            c.TextCentered(94, _message, OricColor.Yellow);
        }
    }

    private void DrawSelect(Canvas c)
    {
        SelectBackdrop(c);
        var icon = Sprite.Of(SpriteData.GPlanet);
        foreach (var planet in GenesisPlanets.All)
        {
            int i = (int)planet;
            var (x, y) = IconPos[i];
            GenesisArt.DrawPlanetIcon(c, planet, x, y);
            bool selected = i == _selected;
            var ink = selected
                ? (Ticks / 10 % 2 == 0 ? OricColor.Yellow : OricColor.White)
                : OricColor.Cyan;
            string name = GenesisPlanets.Name(planet);
            c.Text(x + (icon.Width - name.Length * OricFont.GlyphWidth) / 2,
                y + icon.Height + 2, name, ink);
        }

        c.TextCentered(166, "LEVEL " + _session.Skill, OricColor.Yellow);
        c.TextCentered(186, Game.IsMobile ? "TAP A PLANET TO TRAVEL" : "SPACE TO TRAVEL  ESC FOR MENU", OricColor.White);
        _world.DrawHud(c, 0);
    }

    private static void SelectBackdrop(Canvas c)
    {
        // The original chart is simply the six cyan worlds on black.
        c.Fill(0, 0, Canvas.Width, Canvas.Height, OricColor.Black);
    }
}
