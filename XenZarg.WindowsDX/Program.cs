using System;
using XenZarg.Core;

namespace XenZarg.WindowsDX;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var game = new XenZargGame();
        // --capture <folder> [stills|videos|all] [--mobile] records the store screenshots and videos.
        game.Capture = Core.Demo.CaptureDirector.FromArgs(args);
        game.Run();
    }
}
