using Microsoft.Xna.Framework;

namespace XenZarg.Core.Platform;

/// <summary>Device tilt, reported as gravity in g units relative to the landscape screen.</summary>
public interface ITiltSensor
{
    bool IsAvailable { get; }

    /// <summary>
    /// X: positive when the right-hand side of the screen dips towards the ground.
    /// Y: positive when the top edge of the screen tips away from the player.
    /// </summary>
    Vector2 Read();

    void Start();
    void Stop();
}

/// <summary>Services a platform head can supply to the shared game.</summary>
public interface IPlatformServices
{
    bool IsMobile { get; }
    ITiltSensor Tilt { get; }

    /// <summary>Directory where high scores and settings are stored.</summary>
    string DataDirectory { get; }

    /// <summary>Exit the application (ignored where the platform forbids it).</summary>
    void Quit(Game game);
}

public sealed class NullTiltSensor : ITiltSensor
{
    public bool IsAvailable => false;
    public Vector2 Read() => Vector2.Zero;
    public void Start() { }
    public void Stop() { }
}

/// <summary>Defaults suitable for desktop builds.</summary>
public class DesktopPlatformServices : IPlatformServices
{
    public virtual bool IsMobile => false;
    public virtual ITiltSensor Tilt { get; } = new NullTiltSensor();

    public virtual string DataDirectory
    {
        get
        {
            string root = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root))
                root = System.AppContext.BaseDirectory;
            return System.IO.Path.Combine(root, "XenZarg");
        }
    }

    public virtual void Quit(Game game) => game.Exit();
}
