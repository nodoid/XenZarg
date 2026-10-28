using XenZarg.Core.Graphics;

namespace XenZarg.Core.Genesis;

/// <summary>One playable Xenon 3 planet.</summary>
public abstract class GenesisMission
{
    protected GenesisMission(GenesisWorld world, Planet planet)
    {
        W = world;
        Planet = planet;
    }

    protected GenesisWorld W { get; }

    public Planet Planet { get; }

    public string Name => GenesisPlanets.Name(Planet);

    /// <summary>Specimens (or heads, or waves) collected so far, shown on the status panel.</summary>
    public virtual int Specimens => 0;

    /// <summary>How many are needed to finish.</summary>
    public virtual int Goal => 0;

    public virtual int CompletionBonus => 2000;

    public bool Completed { get; protected set; }
    public bool Failed { get; protected set; }

    /// <summary>When set, hazards cannot cost a shield (attract demos, captures and tests).</summary>
    public bool Invulnerable { get; set; }

    public abstract void Update();

    public abstract void Draw(Canvas c);

    /// <summary>Blows the probe up and loses a shield; the mission ends when the last one goes.</summary>
    protected void Damage(GenesisShip ship)
    {
        if (Invulnerable)
            return;
        W.Blast(ship.CentreX, GenesisShip.Top + 5);
        W.Sound.Play(Audio.Sfx.PlayerDeath);
        if (!W.LoseShield())
            Failed = true;
    }
}
