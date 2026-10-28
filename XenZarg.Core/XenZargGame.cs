using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XenZarg.Core.Audio;
using XenZarg.Core.Genesis;
using XenZarg.Core.Graphics;
using XenZarg.Core.Input;
using XenZarg.Core.Oric;
using XenZarg.Core.Platform;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;
using XenZarg.Core.Xenon;
using XenZarg.Core.Zorgon;

namespace XenZarg.Core;

/// <summary>
/// XenZarg: Xenon 1, Zorgon's Revenge and The Genesis Probe, by PFJ, based on the IJK originals.
/// Renders a 240x224 Oric screen scaled to the window.
/// </summary>
public class XenZargGame : Game
{
    public const int TicksPerSecond = 50;
    public const string Credit = "BY PFJ";
    public const string BasedOn = "BASED ON THE IJK ORIGINALS";

    private readonly GraphicsDeviceManager _graphics;
    private RenderTarget2D _target;
    private SpriteBatch _present;
    private Rectangle _destination;
    private Scene _scene;
    private Scene _pending;
    private bool _paused;
    private KeyboardState _prevKeys;

    public XenZargGame() : this(null)
    {
    }

    public XenZargGame(IPlatformServices platform)
    {
        Platform = platform ?? new DesktopPlatformServices();
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TicksPerSecond);

        if (Platform.IsMobile)
        {
            _graphics.IsFullScreen = true;
            _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = Canvas.Width * 4;
            _graphics.PreferredBackBufferHeight = Canvas.Height * 4;
            IsMouseVisible = true;
        }
    }

    public IPlatformServices Platform { get; }
    public Canvas Canvas { get; private set; }
    public InputManager Input { get; private set; }
    public SoundBank Sound { get; private set; }
    public HighScoreTable HighScores { get; private set; }
    public GameSettings Settings { get; private set; }

    /// <summary>The play-through in progress, if any.</summary>
    public GameSession Session { get; private set; }

    public bool IsMobile => Platform.IsMobile || (Capture?.Mobile ?? false);

    /// <summary>Store-asset capture in progress (see <see cref="Demo.CaptureDirector"/>).</summary>
    public Demo.CaptureDirector Capture { get; set; }

    internal void ShowCaptureScene(Scene scene) => _scene = scene;

    protected override void Initialize()
    {
        Window.Title = "XenZarg - Xenon 1, Zorgon's Revenge & The Genesis Probe";
        if (!IsMobile)
        {
            Window.AllowUserResizing = true;
            FitWindowToDisplay();
        }
        base.Initialize();
    }

    private void FitWindowToDisplay()
    {
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        int scale = 4;
        while (scale > 1 && (Canvas.Width * scale > mode.Width * 0.9 || Canvas.Height * scale > mode.Height * 0.85))
            scale--;
        _graphics.PreferredBackBufferWidth = Canvas.Width * scale;
        _graphics.PreferredBackBufferHeight = Canvas.Height * scale;
        _graphics.ApplyChanges();
    }

    protected override void LoadContent()
    {
        _target = new RenderTarget2D(GraphicsDevice, Canvas.Width, Canvas.Height);
        _present = new SpriteBatch(GraphicsDevice);
        Canvas = new Canvas(GraphicsDevice);
        Input = new InputManager(Platform.Tilt, IsMobile);
        Sound = new SoundBank();
        Settings = GameSettings.Load(Platform.DataDirectory);
        for (int i = 0; i < Settings.Volume; i++)
            Sound.CycleVolume();
        HighScores = new HighScoreTable(Platform.DataDirectory);
        HighScores.Load();
        Platform.Tilt.Start();
        _scene = new TitleScene(this);
        _scene.Enter();
    }

    protected override void UnloadContent()
    {
        Platform.Tilt.Stop();
        Sound?.Dispose();
        Canvas?.Dispose();
        _target?.Dispose();
        _present?.Dispose();
        base.UnloadContent();
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        if (Session != null && _scene is IPausable)
            _paused = true;
        base.OnDeactivated(sender, args);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Capture != null)
        {
            IsFixedTimeStep = false;
            Capture.Update(this);
            base.Update(gameTime);
            return;
        }
        UpdateDestination();
        Input.Update(ToVirtual, IsActive);

        var keys = Keyboard.GetState();
        bool altEnter = keys.IsKeyDown(Keys.Enter) && !_prevKeys.IsKeyDown(Keys.Enter) &&
                        (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
        if (!IsMobile && (altEnter || (keys.IsKeyDown(Keys.F11) && !_prevKeys.IsKeyDown(Keys.F11))))
            _graphics.ToggleFullScreen();
        _prevKeys = keys;

        if (_pending != null)
        {
            _scene.Leave();
            _scene = _pending;
            _pending = null;
            _scene.Enter();
        }

        if (_scene is IPausable)
        {
            if (Input.PausePressed)
                _paused = !_paused;
            if (_paused)
            {
                if (Input.ConfirmPressed)
                    _paused = false;
                else if (Input.BackPressed)
                {
                    _paused = false;
                    ShowMenu();
                }
                base.Update(gameTime);
                return;
            }
        }

        _scene.Tick();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(Color.Black);
        Canvas.Begin();
        _scene.Draw(Canvas);
        if (_paused && Capture == null)
        {
            Canvas.Fill(84, 100, 72, 24, OricColor.Blue);
            Canvas.TextCentered(104, "PAUSED", OricColor.Yellow);
            Canvas.TextCentered(114, IsMobile ? "TAP" : "SPACE", OricColor.White);
        }
        Canvas.End();
        GraphicsDevice.SetRenderTarget(null);
        Capture?.AfterDraw(_target);

        GraphicsDevice.Clear(Color.Black);
        _present.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
        _present.Draw(_target, _destination, Color.White);
        _present.End();
        base.Draw(gameTime);
    }

    private void UpdateDestination()
    {
        var pp = GraphicsDevice.PresentationParameters;
        _destination = ComputeDestination(pp.BackBufferWidth, pp.BackBufferHeight, !IsMobile);
    }

    /// <summary>Largest aspect-correct rectangle for the Oric screen, integer scaled when it fits.</summary>
    public static Rectangle ComputeDestination(int backWidth, int backHeight, bool preferInteger)
    {
        float scale = Math.Min((float)backWidth / Canvas.Width, (float)backHeight / Canvas.Height);
        if (preferInteger && scale >= 1f)
            scale = (float)Math.Floor(scale);
        int w = Math.Max(1, (int)(Canvas.Width * scale));
        int h = Math.Max(1, (int)(Canvas.Height * scale));
        return new Rectangle((backWidth - w) / 2, (backHeight - h) / 2, w, h);
    }

    /// <summary>Converts window / touch coordinates into Oric pixels.</summary>
    public Vector2 ToVirtual(Vector2 screen)
    {
        if (_destination.Width == 0 || _destination.Height == 0)
            return new Vector2(-1, -1);
        return new Vector2(
            (screen.X - _destination.X) * Canvas.Width / _destination.Width,
            (screen.Y - _destination.Y) * Canvas.Height / _destination.Height);
    }

    // ---------------- flow ----------------

    public void SwitchTo(Scene scene) => _pending = scene;

    public void ShowTitle() => SwitchTo(new TitleScene(this));

    public void ShowMenu()
    {
        Session = null;
        SwitchTo(new MenuScene(this));
    }

    public void ShowInstructions(Scene returnTo) => SwitchTo(new InstructionsScene(this, returnTo));

    public void ShowHallOfFame() => SwitchTo(new HallOfFameScene(this, null));

    public void StartNewGame() => StartNewGameAt(Episode.Xenon, playAll: true);

    /// <summary>
    /// Starts a fresh play-through at the chosen game. With <paramref name="playAll"/> the
    /// original three-game sequence continues after each game; otherwise that game alone is played.
    /// </summary>
    public void StartNewGameAt(Episode episode, bool playAll = true)
    {
        Session = new GameSession(Settings.Skill, HighScores.Best);
        Session.ExtraBaseAwarded += () => Sound.Play(Sfx.Bonus);
        Session.Episode = episode;
        Session.PlayAllGames = playAll;
        Input.CalibrateTilt();
        Sound.Play(Sfx.Start);
        SwitchTo(episode switch
        {
            Episode.Zorgon => new ZorgonScene(this, Session),
            Episode.Genesis => new GenesisScene(this, Session),
            _ => new XenonScene(this, Session),
        });
    }

    /// <summary>Called when the Zorgon battle star is destroyed: Zorgon's Revenge is the next level.</summary>
    public void XenonCompleted()
    {
        if (!Session.PlayAllGames)
        {
            GameOver();
            return;
        }
        Session.Episode = Episode.Zorgon;
        SwitchTo(new ZorgonIntroScene(this, Session));
    }

    public void StartZorgon()
    {
        Input.CalibrateTilt();
        SwitchTo(new ZorgonScene(this, Session));
    }

    /// <summary>Princess Roz rescued: Xenon 3 - The Genesis Probe is the third and final game.</summary>
    public void ZorgonCompleted()
    {
        if (!Session.PlayAllGames)
        {
            GameOver();
            return;
        }
        Session.Episode = Episode.Genesis;
        SwitchTo(new GenesisIntroScene(this, Session));
    }

    public void StartGenesis()
    {
        Input.CalibrateTilt();
        Sound.Play(Sfx.Start);
        SwitchTo(new GenesisScene(this, Session));
    }

    /// <summary>The Zorgon mothership destroyed at Radon: start the next, harder cycle with Xenon 1.</summary>
    public void GenesisCompleted()
    {
        if (!Session.PlayAllGames)
        {
            GameOver();
            return;
        }
        Session.CompleteCycle();
        Input.CalibrateTilt();
        SwitchTo(new XenonScene(this, Session));
    }

    public void GameOver()
    {
        var finished = Session;
        Session = null;
        SwitchTo(new HallOfFameScene(this, finished));
    }

    public void Quit() => Platform.Quit(this);
}

/// <summary>Marks scenes that may be paused (gameplay).</summary>
public interface IPausable
{
}

/// <summary>Player preferences persisted between runs.</summary>
public sealed class GameSettings
{
    private const string FileName = "settings.txt";
    private string _directory;

    public int Skill { get; set; } = 5;

    /// <summary>Volume step index as used by <see cref="SoundBank.CycleVolume"/>.</summary>
    public int Volume { get; set; }

    public static GameSettings Load(string directory)
    {
        var s = new GameSettings { _directory = directory };
        try
        {
            string path = Path.Combine(directory, FileName);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var kv = line.Split('=');
                    if (kv.Length != 2 || !int.TryParse(kv[1], out int v))
                        continue;
                    if (kv[0] == "skill")
                        s.Skill = Math.Clamp(v, 1, GameSession.MaxSkill);
                    else if (kv[0] == "volume")
                        s.Volume = Math.Clamp(v, 0, 3);
                }
            }
        }
        catch (Exception)
        {
        }
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, FileName), $"skill={Skill}\nvolume={Volume}\n");
        }
        catch (Exception)
        {
        }
    }
}
