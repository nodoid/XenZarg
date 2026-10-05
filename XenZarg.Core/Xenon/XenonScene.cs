using System;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;

namespace XenZarg.Core.Xenon;

/// <summary>Runs the five Xenon 1 levels with refuelling, then hands over to Zorgon's Revenge.</summary>
public sealed class XenonScene : Scene, IPausable
{
    public enum State { Playing, Refuelling, Dying, Bonus }

    public const int DeathTicks = 100;
    public const int BonusTicks = 120;

    private readonly GameSession _session;
    private readonly XenonWorld _world;
    private XenonLevel _level;
    private RefuelSequence _refuel;
    private int _timer;
    private string _message;
    private bool _warnedLow;

    public XenonScene(XenZargGame game, GameSession session) : base(game)
    {
        _session = session;
        _world = new XenonWorld(session, game.Input, game.Sound, new Random());
        _world.Wave = session.Cycle * 5 + 1;
        _level = CreateLevel(1, _world);
    }

    public State Current { get; private set; } = State.Playing;
    public XenonLevel Level => _level;

    public static XenonLevel CreateLevel(int number, XenonWorld world) => number switch
    {
        1 => new BirdAardLevel(world),
        2 => new AardLevel(world),
        3 => new MeteorLevel(world),
        4 => new RadonLevel(world),
        _ => new BattleStarLevel(world),
    };

    protected override void Update()
    {
        if (Game.Input.BackPressed)
        {
            Game.GameOver();
            return;
        }

        _world.Tick++;
        _world.UpdateBlasts();

        switch (Current)
        {
            case State.Playing:
                UpdatePlaying();
                break;
            case State.Refuelling:
                UpdateRefuelling();
                break;
            case State.Dying:
                if (_world.Tick % 10 == 0)
                    _world.AddBlast(_level.PlayerPosition.x + _world.Rng.Next(-8, 9), _level.PlayerPosition.y + _world.Rng.Next(-8, 9));
                if (--_timer <= 0)
                    AfterDeath();
                break;
            case State.Bonus:
                if (--_timer <= 0)
                    NextLevel();
                break;
        }
    }

    private void UpdatePlaying()
    {
        if (Game.Input.DisengagePressed)
        {
            if (_world.CanRefuel && !(_level is BattleStarLevel b && b.Destroyed))
            {
                _refuel = new RefuelSequence(_world, _level.ShipX);
                Current = State.Refuelling;
                return;
            }
            Game.Sound.Play(Sfx.Warning);
        }

        _level.Update();
        _world.Burn();
        LowFuelWarning();

        if (_world.Fuel <= 0 && !_level.PlayerHit)
        {
            _message = "OUT OF FUEL";
            Die(((int)_level.ShipX + 5, PlayerShip.Top + 9));
            return;
        }
        if (_level.PlayerHit)
        {
            Die(_level.PlayerPosition);
            return;
        }
        if (_level.Completed)
        {
            int bonus = _level.CompletionBonus;
            _session.AddScore(bonus);
            _message = _level.CompletionMessage;
            _timer = BonusTicks;
            _world.ClearProjectiles();
            Current = State.Bonus;
            Game.Sound.Play(Sfx.Bonus);
        }
    }

    private void LowFuelWarning()
    {
        if (_world.Fuel < XenonWorld.FuelYellow && !_warnedLow)
        {
            _warnedLow = true;
            Game.Sound.Play(Sfx.Warning);
        }
        if (_world.Fuel >= XenonWorld.FuelYellow)
            _warnedLow = false;
        if (_world.Fuel < XenonWorld.FuelRed && _world.Tick % 100 == 0)
            Game.Sound.Play(Sfx.Warning);
    }

    private void UpdateRefuelling()
    {
        _refuel.Update();
        if (_refuel.Current != RefuelSequence.State.Done)
            return;
        _level.ShipX = _refuel.ShipX;
        if (_refuel.Crashed)
        {
            var at = ((int)_refuel.ShipX + 5, (int)_refuel.ShipY + 9);
            _refuel = null;
            Current = State.Playing;
            Die(at);
            return;
        }
        _refuel = null;
        Current = State.Playing;
    }

    private void Die((int x, int y) at)
    {
        _deathAt = at;
        Current = State.Dying;
        _timer = DeathTicks;
        _world.ClearProjectiles();
        Game.Sound.Play(Sfx.PlayerDeath);
        _world.AddBlast(at.x, at.y);
    }

    private (int x, int y) _deathAt;

    private void AfterDeath()
    {
        _session.LoseBase();
        _message = null;
        if (_session.IsGameOver)
        {
            Game.GameOver();
            return;
        }
        if (_world.Fuel <= 0)
            _world.Fuel = XenonWorld.MaxFuel;
        _level.Respawn();
        Game.Input.CalibrateTilt();
        Current = State.Playing;
    }

    private void NextLevel()
    {
        _message = null;
        if (_level.Number >= 5)
        {
            Game.XenonCompleted();
            return;
        }
        float shipX = _level.ShipX;
        _level = CreateLevel(_level.Number + 1, _world);
        _level.ShipX = shipX;
        _world.Wave++;
        Current = State.Playing;
    }

    public override void Draw(Canvas c)
    {
        _world.DrawFuel(c);
        if (Current == State.Refuelling)
            _refuel.Draw(c);
        else
            _level.Draw(c);
        _world.DrawCommon(c);

        if (Current == State.Dying)
        {
            var star = Sprite.Of(SpriteData.Explosion);
            var ink = (OricColor)(1 + _timer / 3 % 7);
            c.DrawTinted(star, _deathAt.x - 5, _deathAt.y - 6, ink);
        }

        if (_message != null)
            c.TextCentered(90, _message, OricColor.Yellow);
        else if (Current == State.Playing && _world.CanRefuel && _world.Tick / 25 % 2 == 0)
            c.TextAt(12, 0, Game.IsMobile ? "TAP GAUGE TO REFUEL" : "DISENGAGE TO REFUEL", OricColor.White);

        _world.DrawHud(c);
    }
}
