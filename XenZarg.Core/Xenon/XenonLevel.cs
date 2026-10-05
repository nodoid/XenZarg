using XenZarg.Core.Graphics;

namespace XenZarg.Core.Xenon;

/// <summary>One of the five Xenon 1 levels.</summary>
public abstract class XenonLevel
{
    protected XenonLevel(XenonWorld world)
    {
        W = world;
    }

    protected XenonWorld W { get; }

    /// <summary>Level number 1..5 as in the instructions.</summary>
    public abstract int Number { get; }

    public bool Completed { get; protected set; }

    /// <summary>Set by the level when the player has been destroyed this tick.</summary>
    public bool PlayerHit { get; protected set; }

    /// <summary>Where the player exploded (for the death animation).</summary>
    public (int x, int y) PlayerPosition { get; protected set; }

    public virtual int CompletionBonus => 1000;

    /// <summary>Text shown when the level is cleared.</summary>
    public virtual string CompletionMessage => "BONUS " + CompletionBonus;

    public abstract void Update();

    public abstract void Draw(Canvas c);

    /// <summary>Restart after losing a base: player back to the start, projectiles cleared.</summary>
    public virtual void Respawn()
    {
        PlayerHit = false;
        W.ClearProjectiles();
    }

    /// <summary>Horizontal position of the player's ship, kept across a refuelling trip.</summary>
    public virtual float ShipX { get => Canvas.Width / 2f; set { } }

    /// <summary>When set the player cannot be destroyed (attract demos and store captures).</summary>
    public bool Invulnerable { get; set; }

    protected void Kill(int x, int y)
    {
        if (PlayerHit || Invulnerable)
            return;
        PlayerHit = true;
        PlayerPosition = (x, y);
    }
}
