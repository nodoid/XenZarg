using XenZarg.Core.Graphics;

namespace XenZarg.Core.Scenes;

/// <summary>A full screen of the game. Updated at a fixed 50 ticks per second, like a PAL Oric.</summary>
public abstract class Scene
{
    protected Scene(XenZargGame game)
    {
        Game = game;
    }

    protected XenZargGame Game { get; }

    /// <summary>Ticks since the scene was entered.</summary>
    protected int Ticks { get; private set; }

    public virtual void Enter() { }

    public virtual void Leave() { }

    public void Tick()
    {
        Ticks++;
        Update();
    }

    protected abstract void Update();

    public abstract void Draw(Canvas canvas);
}
