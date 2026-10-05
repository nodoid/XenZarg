using System;
using System.Collections.Generic;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Zorgon.Missions;

/// <summary>
/// The chasm before Zorgon's tower: jump to hit the crane's button and it lowers the magic stone
/// you brought back into the gap as the next section of the bridge. Zorgon's birds drop stones.
/// </summary>
public sealed class BridgeScreen : PlatformMission
{
    public const int GroundTop = 189;
    public const int GapX0 = 138, GapX1 = 185;
    public const int PartWidth = 12;
    public const int PartY = 183;
    public const int HeroLimitX = 101 + 12;
    public const int ButtonRow = 154;
    public const int LowerSteps = 21;
    public const int HookTopY = PartY - 9 - LowerSteps;

    private static readonly string[][] BirdFrames = [SpriteData.ZBridgeAttackBirdF0, SpriteData.ZBridgeAttackBirdF1, SpriteData.ZBridgeAttackBirdF2];
    private static readonly string[][] ZorgonFrames = [SpriteData.ZZorgonOnTowerF0, SpriteData.ZZorgonOnTowerF1, SpriteData.ZZorgonOnTowerF2, SpriteData.ZZorgonOnTowerF1];

    public sealed class AttackBird
    {
        public float X, Y, Travel;
        public bool Dropped;
    }

    private readonly int _partsLaid;
    private readonly List<AttackBird> _birds = new();
    private readonly List<(float x, float y)> _stones = new();
    private bool _lowering;
    private float _hookDrop;

    public BridgeScreen(ZorgonWorld world, int partsAlreadyLaid) : base(world, 12, GroundTop)
    {
        _partsLaid = partsAlreadyLaid;
        Surfaces.Add(new Surface(0, GapX0 - 1, GroundTop));
        Hero.MinX = 8;
        Hero.MaxX = HeroLimitX;
        foreach (var (x, y) in new[] { (48, 105), (84, 87), (144, 117) })
            _birds.Add(new AttackBird { X = x, Y = y, Travel = W.Rng.Next(100) });
    }

    public override OricColor HudPaper => OricColor.Blue;
    public override OricColor HudInk => OricColor.Yellow;
    public override int StartBonus => 2000;
    public bool Lowering => _lowering;
    public int PartsLaid => _partsLaid + (Completed ? 1 : 0);

    public override void Update()
    {
        UpdateHero();
        if (!_lowering && Hero.Airborne && Hero.Top <= ButtonRow && Hero.X >= HeroLimitX - 4)
        {
            _lowering = true;
            W.Sound.Play(Sfx.Lift);
        }
        if (_lowering)
        {
            _hookDrop += ZorgonWorld.PerTick(2.67f) * 1.5f;
            if (W.Tick % 25 == 0)
                W.Sound.Play(Sfx.Lift);
            if (_hookDrop >= LowerSteps)
            {
                Completed = true;
                W.Sound.Play(Sfx.Bridge);
            }
        }
        UpdateBirds();
    }

    private void UpdateBirds()
    {
        float speed = ZorgonWorld.PerTick(7.6f + W.Hardness);
        foreach (var b in _birds)
        {
            b.X -= speed;
            b.Travel += speed;
            if (b.Travel >= 168)
            {
                b.Travel = 0;
                b.X += 168;
                b.Dropped = false;
            }
            if (!b.Dropped && b.Travel > 168 - 54 * 2 && W.Chance(0.02))
            {
                b.Dropped = true;
                _stones.Add((b.X + 14, b.Y + 14));
            }
            if (Hero.Touches(BirdSprite(b), (int)b.X, (int)b.Y))
                Kill();
        }
        var stone = Sprite.Of(SpriteData.ZStone);
        for (int i = _stones.Count - 1; i >= 0; i--)
        {
            var (x, y) = _stones[i];
            y += ZorgonWorld.PerTick(30f);
            _stones[i] = (x, y);
            if (Hero.Touches(stone, (int)x, (int)y))
            {
                Kill();
                _stones.RemoveAt(i);
            }
            else if (y > GroundTop)
            {
                _stones.RemoveAt(i);
            }
        }
    }

    private Sprite BirdSprite(AttackBird b) => Sprite.Of(BirdFrames[(W.Tick / 8 + (int)b.Y) % 3]);

    public override void Draw(Canvas c)
    {
        c.Draw(SpriteData.BridgeBackground, 0, 0);
        DrawCastleFolk(c, W.Tick);
        for (int i = 0; i < _partsLaid; i++)
            c.Draw(SpriteData.ZBridgePartLaid, GapX0 + i * PartWidth, PartY);

        // Hook and the stone it carries, lowered into the next slot.
        int hookX = GapX0 + _partsLaid * PartWidth;
        int hookY = HookTopY + (int)Math.Min(_hookDrop, LowerSteps);
        c.Fill(hookX + 6, 159, 1, Math.Max(0, hookY - 159), OricColor.White);
        c.Draw(SpriteData.ZCraneHookPiece, hookX - 6, hookY);
        c.Draw(SpriteData.ZBridgePartLaid, hookX, hookY + 9);

        foreach (var b in _birds)
            c.Draw(BirdSprite(b), (int)b.X, (int)b.Y);
        foreach (var (x, y) in _stones)
            c.Draw(SpriteData.ZStone, (int)x, (int)y);
        Hero.Draw(c);
    }

    /// <summary>Zorgon guarding Princess Roz on the tower top, her HELP flashing.</summary>
    public static void DrawCastleFolk(Canvas c, int tick)
    {
        c.Draw(ZorgonFrames[tick / 10 % ZorgonFrames.Length], 170, 18);
        c.Draw(SpriteData.ZPrincessRoz, 197, 13);
        if (tick / 20 % 2 == 0)
            c.Draw(SpriteData.ZHelpBubble, 210, 0);
    }
}
