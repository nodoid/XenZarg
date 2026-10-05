using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;
using XenZarg.Core.Zorgon.Missions;

namespace XenZarg.Core.Zorgon;

/// <summary>Runs Zorgon's Revenge: mission select, the four missions, the bridge, the castle and the finale.</summary>
public sealed class ZorgonScene : Scene, IPausable
{
    public enum State { Select, Preparing, Playing, Heart }

    public const int PrepareTicks = 120;
    public const int HeartTicks = 250;

    private readonly GameSession _session;
    private readonly ZorgonWorld _world;
    private readonly ZorgonCampaign _campaign;
    private ZorgonMission _mission;
    private int _door;
    private int _highlight;
    private int _timer;

    public ZorgonScene(XenZargGame game, GameSession session) : base(game)
    {
        _session = session;
        _world = new ZorgonWorld(session, game.Input, game.Sound, new Random());
        _campaign = new ZorgonCampaign(_world.Rng);
    }

    public State Current { get; private set; } = State.Select;

    public static ZorgonMission CreateMission(MissionKind kind, ZorgonWorld world) => kind switch
    {
        MissionKind.Spider => new SpiderMission(world),
        MissionKind.Reptile => new ReptileMission(world),
        MissionKind.Ship => new ShipMission(world),
        _ => new BirdMission(world),
    };

    protected override void Update()
    {
        var input = Game.Input;
        if (input.BackPressed)
        {
            Game.GameOver();
            return;
        }
        _world.Tick++;

        switch (Current)
        {
            case State.Select:
                UpdateSelect();
                break;
            case State.Preparing:
                if (--_timer <= 0)
                    Begin(CreateMission(_campaign.MissionBehind(_door), _world));
                break;
            case State.Playing:
                UpdatePlaying();
                break;
            case State.Heart:
                if (--_timer <= 0)
                    Game.ZorgonCompleted();
                break;
        }
    }

    private void UpdateSelect()
    {
        var input = Game.Input;
        if (input.LeftPressed || input.UpPressed)
            _highlight = _campaign.NextOpenDoor(_highlight, -1);
        if (input.RightPressed || input.DownPressed)
            _highlight = _campaign.NextOpenDoor(_highlight, 1);
        if (_campaign.IsDone(_highlight))
            _highlight = _campaign.NextOpenDoor(_highlight, 1);

        int chosen = -1;
        foreach (char ch in input.Typed)
            if (ch >= '1' && ch <= '4')
                chosen = ch - '1';
        foreach (var tap in input.Taps)
            if (tap.Y >= 0 && tap.Y < 200)
                chosen = (tap.X >= 120 ? 1 : 0) + (tap.Y >= 100 ? 2 : 0);
        if (chosen < 0 && input.Taps.Count == 0 && input.FirePressed)
            chosen = _highlight;

        if (chosen >= 0 && _campaign.Choose(chosen))
        {
            _door = chosen;
            _highlight = chosen;
            Current = State.Preparing;
            _timer = PrepareTicks;
            Game.Sound.Play(Sfx.Warning);
        }
    }

    private void Begin(ZorgonMission mission)
    {
        _mission = mission;
        _world.Bonus = mission.StartBonus;
        Current = State.Playing;
        Game.Input.CalibrateTilt();
    }

    private void UpdatePlaying()
    {
        _mission.Update();
        if (_mission.BonusCountsDown)
            _world.CountDownBonus();

        if (_mission.Completed)
        {
            if (_mission.AwardsBonus)
                _session.AddScore(_world.Bonus);
            Game.Sound.Play(Sfx.Bonus);
            switch (_mission)
            {
                case BridgeScreen:
                    _campaign.BridgePartLaid();
                    if (_campaign.BridgeComplete)
                        Begin(new KingScreen(_world));
                    else
                        Current = State.Select;
                    break;
                case KingScreen:
                    Current = State.Heart;
                    _timer = HeartTicks;
                    Game.Sound.Play(Sfx.Victory);
                    break;
                default:
                    _campaign.MissionWon(_door);
                    Begin(new BridgeScreen(_world, _campaign.PartsLaid));
                    break;
            }
            return;
        }

        if (_mission.Failed)
        {
            _session.LoseBase();
            if (_session.IsGameOver)
            {
                Game.GameOver();
                return;
            }
            // The bridge and castle are replayed; a failed mission sends you back to the doors.
            switch (_mission)
            {
                case BridgeScreen:
                    Begin(new BridgeScreen(_world, _campaign.PartsLaid));
                    break;
                case KingScreen:
                    Begin(new KingScreen(_world));
                    break;
                default:
                    Current = State.Select;
                    break;
            }
        }
    }

    public override void Draw(Canvas c)
    {
        switch (Current)
        {
            case State.Select:
            case State.Preparing:
                DrawSelect(c);
                break;
            case State.Playing:
                _mission.Draw(c);
                _world.DrawHud(c, _mission.HudPaper, _mission.HudInk);
                break;
            case State.Heart:
                c.Draw(SpriteData.ZCompletionHeart, 83, 59);
                _world.DrawHud(c, OricColor.Yellow, OricColor.Blue);
                break;
        }
    }

    private static readonly (int x, int y)[] DigitPos = [(54, 84), (174, 84), (54, 186), (174, 186)];

    private void DrawSelect(Canvas c)
    {
        for (int door = 0; door < ZorgonCampaign.Doors; door++)
        {
            int qx = door % 2 * 120, qy = door / 2 * 100;
            bool open = _campaign.IsOpened(door) && (Current == State.Preparing ? door == _door || _campaign.IsDone(door) : true);
            bool done = _campaign.IsDone(door);
            if (!open && !done)
                c.Fill(qx, qy, 120, 100, OricColor.Magenta);
            else
            {
                if (done)
                    c.Fill(qx, qy, 120, 100, OricColor.Blue);
                DrawIcon(c, _campaign.MissionBehind(door), qx + 60, qy + 40);
            }
            var (dx, dy) = DigitPos[door];
            bool flash = Current == State.Select && door == _highlight && Ticks / 10 % 2 == 0;
            c.Text(dx - 1, dy - 1, (door + 1).ToString(), flash ? OricColor.Yellow : OricColor.White);
        }
        c.Fill(0, 200, Canvas.Width, 24, OricColor.Black);
        string text = Current == State.Preparing ? "Prepare to meet thy doom" : "Select mission .....";
        c.TextCentered(208, text, OricColor.White);
    }

    private void DrawIcon(Canvas c, MissionKind kind, int cx, int cy)
    {
        var s = Sprite.Of(kind switch
        {
            MissionKind.Spider => SpriteData.ZSpiderF1,
            MissionKind.Ship => SpriteData.ZPlayerShip,
            MissionKind.Reptile => SpriteData.ZReptileQuadnogQ1Anim0,
            _ => SpriteData.ZBirdMissionBirdF5,
        });
        c.Draw(s, cx - s.Width / 2, cy - s.Height / 2);
    }
}
