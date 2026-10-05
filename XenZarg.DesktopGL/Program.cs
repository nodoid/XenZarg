using XenZarg.Core;

namespace XenZarg.DesktopGL;

internal static class Program
{
    private static void Main(string[] args)
    {
        using var game = new XenZargGame();
        // --capture <folder> [stills|videos|all] [--mobile] records the store screenshots and videos.
        game.Capture = Core.Demo.CaptureDirector.FromArgs(args);
        game.Run();
    }
}
