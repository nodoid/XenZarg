using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using XenZarg.Core.Audio;
using XenZarg.Core.Scenes;
using XenZarg.Core.Session;
using XenZarg.Core.Zorgon;

namespace XenZarg.Core.Demo;

/// <summary>
/// Store asset capture: plays a list of scenes, saving native 240x224 frames as PNG (stills,
/// or every tick for videos) and recording the sound effects so a matching audio track can be
/// mixed. Start with <c>--capture &lt;folder&gt; [stills|videos|all] [--mobile]</c>.
/// </summary>
public sealed class CaptureDirector : ISoundPlayer
{
    public sealed record Shot(string Name, Func<XenZargGame, ISoundPlayer, Scene> Create, int SkipTicks, int Frames = 1, Func<Scene, bool> Ready = null);

    private readonly string _folder;
    private readonly List<Shot> _shots;
    private int _index = -1;
    private Scene _scene;
    private int _skipped;
    private int _frame;
    private bool _saveThisFrame;
    private readonly List<(int frame, Sfx sfx)> _events = new();

    public CaptureDirector(string folder, List<Shot> shots, bool mobile)
    {
        _folder = folder;
        _shots = shots;
        Mobile = mobile;
    }

    public bool Mobile { get; }
    public bool Finished { get; private set; }

    public static CaptureDirector FromArgs(string[] args)
    {
        int i = Array.IndexOf(args, "--capture");
        if (i < 0 || i + 1 >= args.Length)
            return null;
        string folder = args[i + 1];
        string what = i + 2 < args.Length && !args[i + 2].StartsWith("--") ? args[i + 2] : "all";
        bool mobile = Array.IndexOf(args, "--mobile") >= 0;
        var shots = new List<Shot>();
        if (what is "stills" or "all")
            shots.AddRange(Stills());
        if (what is "videos" or "all")
            shots.AddRange(Videos());
        return new CaptureDirector(folder, shots, mobile);
    }

    private static Scene Xenon(XenZargGame g, ISoundPlayer s, int level) => new XenonDemoScene(g, level, s);
    private static Scene Zorgon(XenZargGame g, ISoundPlayer s, string screen) => new ZorgonDemoScene(g, screen, s);

    private static bool Riding(Scene s) =>
        s is ZorgonDemoScene z && z.Mission is PlatformMission p && p.Hero.Current == Hero.State.Riding && p.Hero.Top < 150;

    private static bool ShipBusy(Scene s) =>
        s is ZorgonDemoScene z && z.Mission is Zorgon.Missions.ShipMission m && m.Aliens.Count >= 3 && m.Beams.Count >= 1 && !m.Completed;

    private static bool CannonFiring(Scene s) =>
        s is ZorgonDemoScene z && z.Mission is Zorgon.Missions.KingScreen k && k.Shots.Count > 0 && k.Shots[0].Y < 100;

    /// <summary>The still screenshots, in store order.</summary>
    public static IEnumerable<Shot> Stills()
    {
        yield return new Shot("01-xenon-bird-aards", (g, s) => Xenon(g, s, 1), 260);
        yield return new Shot("02-xenon-title", (g, s) => new TitleScene(g), 60);
        yield return new Shot("03-xenon-large-aards", (g, s) => Xenon(g, s, 2), 420);
        yield return new Shot("04-planet-radon", (g, s) => Xenon(g, s, 4), 900);
        yield return new Shot("05-zorgon-battle-star", (g, s) => Xenon(g, s, 5), 520);
        yield return new Shot("06-zorgon-spider", (g, s) => Zorgon(g, s, "spider"), 100, Ready: Riding);
        yield return new Shot("07-zorgon-quadnogs", (g, s) => Zorgon(g, s, "reptile"), 1700);
        yield return new Shot("08-zorgon-ship", (g, s) => Zorgon(g, s, "ship"), 300, Ready: ShipBusy);
        yield return new Shot("09-zorgon-castle", (g, s) => Zorgon(g, s, "king"), 100, Ready: CannonFiring);
        yield return new Shot("10-zorgon-title", (g, s) => new ZorgonIntroScene(g, new GameSession(5, 0)), 560);
        yield return new Shot("11-meteorites", (g, s) => Xenon(g, s, 3), 420);
        yield return new Shot("12-refuelling", (g, s) => new XenonDemoScene(g, 1, s, refuel: true), 110);
        yield return new Shot("13-zorgon-bird", (g, s) => Zorgon(g, s, "bird"), 100, Ready: Riding);
        yield return new Shot("14-zorgon-bridge", (g, s) => Zorgon(g, s, "bridge"), 480);
        yield return new Shot("15-mission-select", (g, s) => new ZorgonScene(g, new GameSession(5, 0)), 20);
        yield return new Shot("16-rescued", (g, s) => Zorgon(g, s, "heart"), 5);
        yield return new Shot("17-instructions", (g, s) => new InstructionsScene(g, null), 5);
        yield return new Shot("18-menu", (g, s) => new MenuScene(g), 5);
    }

    /// <summary>One continuous montage of the game in action (about 30 seconds).</summary>
    public static IEnumerable<Shot> Videos()
    {
        yield return new Shot("video/01", (g, s) => new TitleScene(g), 40, 90);
        yield return new Shot("video/02", (g, s) => Xenon(g, s, 1), 120, 200);
        yield return new Shot("video/03", (g, s) => Xenon(g, s, 2), 300, 170);
        yield return new Shot("video/04", (g, s) => Xenon(g, s, 4), 700, 170);
        yield return new Shot("video/05", (g, s) => Xenon(g, s, 5), 380, 170);
        yield return new Shot("video/06", (g, s) => Zorgon(g, s, "spider"), 0, 220);
        yield return new Shot("video/07", (g, s) => Zorgon(g, s, "ship"), 700, 170);
        yield return new Shot("video/08", (g, s) => Zorgon(g, s, "bird"), 520, 200);
        yield return new Shot("video/09", (g, s) => Zorgon(g, s, "king"), 0, 170);
    }

    public void Play(Sfx sfx, float pitch = 0f)
    {
        if (_index >= 0 && _shots[_index].Frames > 1 && _skipped >= _shots[_index].SkipTicks)
            _events.Add((_frame, sfx));
    }

    /// <summary>Called instead of the normal update while capturing.</summary>
    public void Update(XenZargGame game)
    {
        if (Finished)
            return;
        if (_scene == null)
        {
            if (++_index >= _shots.Count)
            {
                Finished = true;
                game.Exit();
                return;
            }
            _scene = _shots[_index].Create(game, this);
            _scene.Enter();
            game.ShowCaptureScene(_scene);
            _skipped = 0;
            _frame = 0;
            _events.Clear();
        }

        var shot = _shots[_index];
        // Fast-forward to the moment of interest without drawing.
        while (_skipped < shot.SkipTicks || (_frame == 0 && shot.Ready != null && !shot.Ready(_scene) && _skipped < shot.SkipTicks + 30000))
        {
            _scene.Tick();
            _skipped++;
        }
        _scene.Tick();
        _saveThisFrame = true;
    }

    /// <summary>Called after the Oric screen has been drawn.</summary>
    public void AfterDraw(RenderTarget2D target)
    {
        if (!_saveThisFrame || _index < 0 || _index >= _shots.Count)
            return;
        _saveThisFrame = false;
        var shot = _shots[_index];
        string path = shot.Frames > 1
            ? Path.Combine(_folder, shot.Name, $"f{_frame:D5}.png")
            : Path.Combine(_folder, "stills", shot.Name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = File.Create(path))
            target.SaveAsPng(file, target.Width, target.Height);
        _frame++;
        if (_frame >= shot.Frames)
        {
            if (shot.Frames > 1)
                WriteAudio(Path.Combine(_folder, shot.Name + ".wav"), shot.Frames);
            Console.WriteLine($"captured {shot.Name}");
            _scene = null;
        }
    }

    private void WriteAudio(string path, int frames)
    {
        var clips = new Dictionary<Sfx, short[]>();
        foreach (var (id, pcm) in SoundBank.Build())
            clips[id] = pcm;
        int total = frames * Synth.SampleRate / XenZargGame.TicksPerSecond;
        var mix = new int[total];
        foreach (var (frame, sfx) in _events)
        {
            int start = frame * Synth.SampleRate / XenZargGame.TicksPerSecond;
            var clip = clips[sfx];
            for (int i = 0; i < clip.Length && start + i < total; i++)
                mix[start + i] += clip[i] * 6 / 10;
        }
        var samples = new short[total];
        for (int i = 0; i < total; i++)
            samples[i] = (short)Math.Clamp(mix[i], short.MinValue, short.MaxValue);
        Wav.Write(path, samples, Synth.SampleRate);
    }
}

public static class Wav
{
    /// <summary>Writes 16-bit mono PCM as a .wav file.</summary>
    public static void Write(string path, short[] samples, int rate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var w = new BinaryWriter(File.Create(path));
        int bytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + bytes);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(rate);
        w.Write(rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8.ToArray());
        w.Write(bytes);
        foreach (var s in samples)
            w.Write(s);
    }
}
