using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon.Missions;

/// <summary>
/// The Quadnogs' tower: kill each Quadnog by jumping into one of its white shots (red shots kill
/// you, green shots must be jumped), get past the cycling laser beams to the switch, then ride the
/// lift to the top and walk home.
/// </summary>
public sealed class ReptileMission : PlatformMission
{
    public const int FloorTop = 189, MiddleTop = 126, UpperTop = 63;
    public const int BeamTop = 80, BeamBottom = 124, BeamWidth = 6;
    public static readonly int[] BeamX = [60, 96, 132, 168];
    public const int HomeX = 36;

    public sealed class Quadnog
    {
        public int X, Y;          // top-left of the 48x40 animation frame
        public bool Alive = true;
        public bool Active;
        public int DeathTimer;
        public int ShotDir;       // direction its shots travel
        public int HeadRow, ShinRow, MuzzleX;
    }

    public sealed class Shot
    {
        public float X;
        public int Y;
        public int Dir;
        public OricColor Color;
        public bool Bounced;
        public Quadnog Owner;
    }

    private static readonly string[][] QuadnogFrames =
    [
        SpriteData.ZReptileQuadnogQ1Anim0, SpriteData.ZReptileQuadnogQ1Anim1, SpriteData.ZReptileQuadnogQ1Anim2, SpriteData.ZReptileQuadnogQ1Anim3,
        SpriteData.ZReptileQuadnogQ1Anim4, SpriteData.ZReptileQuadnogQ1Anim5, SpriteData.ZReptileQuadnogQ1Anim6, SpriteData.ZReptileQuadnogQ1Anim7,
    ];

    private readonly Lift _lift;
    private readonly Quadnog _q1, _q2;
    private readonly List<Shot> _shots = new();
    private readonly int[] _beamTicks;
    private int _beamIndex;
    private int _beamTimer;
    private int _whiteRedTimer;
    private int _greenTimer;
    private bool _nextWhite = true;
    private readonly float _shotSpeed = ZorgonWorld.PerTick(33f);

    public ReptileMission(ZorgonWorld world) : base(world, 12, FloorTop)
    {
        Surfaces.Add(new Surface(0, 209, FloorTop));
        Surfaces.Add(new Surface(0, 209, MiddleTop));
        Surfaces.Add(new Surface(0, 209, UpperTop));
        Hero.MinX = 8;
        int d = world.Hardness;
        _lift = new Lift(Lift.Lowest, ZorgonWorld.PerTick(16.7f + d), -1);
        _q1 = new Quadnog { X = 162, Y = FloorTop - 40, Active = true, ShotDir = -1, HeadRow = 164, ShinRow = 181, MuzzleX = 150 };
        _q2 = new Quadnog { X = 30, Y = UpperTop - 40, ShotDir = 1, HeadRow = 38, ShinRow = 55, MuzzleX = 76 };
        _beamTicks = new int[BeamX.Length];
        for (int k = 0; k < _beamTicks.Length; k++)
            _beamTicks[k] = Math.Max(20, (int)((14 + 6 * k) * 30 / 333f * XenZargGame.TicksPerSecond / ZorgonWorld.Speed) - d * 3);
        _beamTimer = _beamTicks[0];
        _whiteRedTimer = 60;
        _greenTimer = 100;
    }

    public override OricColor HudPaper => OricColor.Cyan;
    public override OricColor HudInk => OricColor.Black;
    public override int StartBonus => 8990;

    public bool SwitchThrown { get; private set; }
    public Quadnog Q1 => _q1;
    public Quadnog Q2 => _q2;
    public IReadOnlyList<Shot> Shots => _shots;

    /// <summary>Index of the lit beam, or -1 when all are dark.</summary>
    public int LitBeam => SwitchThrown || _beamIndex >= BeamX.Length ? -1 : _beamIndex;

    public Lift Lift => _lift;

    public override void Update()
    {
        _lift.Update();
        UpdateBeams();
        UpdateQuadnog(_q1);
        if (!_q2.Active && _q2.Alive && Hero.Feet <= UpperTop + 1 && Hero.X <= 31 * 6 + 12)
            _q2.Active = true;
        UpdateQuadnog(_q2);
        UpdateShots();

        var surfaces = new List<Surface>(Surfaces) { _lift.Surface };
        bool landed = UpdateHero(surfaces);
        _lift.Carry(Hero, landed);
        if (Hero.Carrier == _lift && _lift.Wrapped)
            Hero.Die(W.Sound);

        int lit = LitBeam;
        if (lit >= 0 && Hero.TouchesRect(BeamX[lit], BeamTop, BeamWidth, BeamBottom - BeamTop + 1))
            Kill();

        // The switch hangs under the upper platform at the far left of the middle level.
        if (!SwitchThrown && Hero.Airborne && Hero.Feet <= MiddleTop && Hero.Feet > UpperTop + 10 && Hero.X <= 26 && Hero.Top <= 90)
        {
            SwitchThrown = true;
            W.Sound.Play(Sfx.Pickup);
        }

        if (SwitchThrown && !_q2.Alive && Hero.Current == Hero.State.Standing && Hero.Feet == UpperTop && Hero.X <= HomeX)
            Completed = true;
    }

    private void UpdateBeams()
    {
        if (SwitchThrown)
            return;
        if (--_beamTimer > 0)
            return;
        _beamIndex = (_beamIndex + 1) % (BeamX.Length + 1);
        _beamTimer = _beamIndex < BeamX.Length ? _beamTicks[_beamIndex] : _beamTicks[0];
    }

    private void UpdateQuadnog(Quadnog q)
    {
        if (!q.Alive)
        {
            if (q.DeathTimer > 0)
                q.DeathTimer--;
            return;
        }
        if (Hero.Touches(QuadnogSprite(q), q.X, q.Y))
            Kill();
        if (!q.Active)
            return;

        if (--_whiteRedTimer <= 0)
        {
            _shots.Add(new Shot { X = q.MuzzleX, Y = q.HeadRow, Dir = q.ShotDir, Color = _nextWhite ? OricColor.White : OricColor.Red, Owner = q });
            _nextWhite = !_nextWhite;
            _whiteRedTimer = Math.Max(40, 84 - W.Hardness * 4);
            W.Sound.Play(Sfx.Bomb);
        }
        if (--_greenTimer <= 0)
        {
            _shots.Add(new Shot { X = q.MuzzleX, Y = q.ShinRow, Dir = q.ShotDir, Color = OricColor.Green, Owner = q });
            _greenTimer = Math.Max(35, 65 - W.Hardness * 3);
        }
    }

    private void UpdateShots()
    {
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            s.X += s.Dir * _shotSpeed;
            if (s.X < -6 || s.X > 215)
            {
                _shots.RemoveAt(i);
                continue;
            }
            if (s.Bounced)
            {
                var q = s.Owner;
                if (q.Alive && s.X + 6 >= q.X + 8 && s.X <= q.X + 40)
                {
                    q.Alive = false;
                    q.DeathTimer = 60;
                    _shots.RemoveAt(i);
                    W.Sound.Play(Sfx.EnemyHit);
                }
                continue;
            }
            if (!Hero.TouchesRect((int)s.X, s.Y, 6, 1))
                continue;
            if (s.Color == OricColor.White && Hero.Airborne)
            {
                // Headed back: the white shot rebounds on to its Quadnog.
                s.Bounced = true;
                s.Dir = -s.Dir;
                W.Sound.Play(Sfx.Rope);
            }
            else
            {
                Kill();
                _shots.RemoveAt(i);
            }
        }
    }

    private static Sprite QuadnogSprite(Quadnog q) => Sprite.Of(QuadnogFrames[0]);

    private Sprite QuadnogFrame(Quadnog q) => Sprite.Of(QuadnogFrames[(W.Tick / 5 + q.X) % QuadnogFrames.Length]);

    public override void Draw(Canvas c)
    {
        c.Draw(SpriteData.ReptileBackground, 0, 0);

        int lit = LitBeam;
        if (lit >= 0)
        {
            // Rainbow beam with scrolling colours.
            for (int y = BeamTop; y <= BeamBottom; y++)
                c.Fill(BeamX[lit], y, BeamWidth, 1, (OricColor)(1 + (y + W.Tick) % 7));
        }
        if (SwitchThrown)
            c.Draw(SpriteData.ZBridgePartBlock, 6, 57);

        foreach (var q in new[] { _q1, _q2 })
        {
            if (q.Alive)
                c.Draw(QuadnogFrame(q), q.X, q.Y);
            else if (q.DeathTimer > 0 && q.DeathTimer / 4 % 2 == 0)
                c.DrawTinted(Sprite.Of(QuadnogFrames[0]), q.X, q.Y, OricColor.Yellow);
            else
                c.Draw(SpriteData.ZReptileDeadQuadnog, q.X + 6, q.Y + 20);
        }

        foreach (var s in _shots)
            c.Fill((int)s.X, s.Y, 6, 1, s.Color);

        _lift.Draw(c);
        Hero.Draw(c);
    }
}
