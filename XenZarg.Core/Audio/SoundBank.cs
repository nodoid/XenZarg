using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace XenZarg.Core.Audio;

/// <summary>Builds every sound effect at start-up from <see cref="Synth"/> and plays them.</summary>
public sealed class SoundBank : ISoundPlayer, IDisposable
{
    private readonly Dictionary<Sfx, SoundEffect> _effects = new();
    private readonly bool _enabled;
    private static readonly float[] VolumeSteps = [0.6f, 0.3f, 0f, 1f];
    private int _volumeIndex;

    public SoundBank()
    {
        try
        {
            foreach (var (id, pcm) in Build())
                _effects[id] = new SoundEffect(Synth.ToBytes(pcm), Synth.SampleRate, AudioChannels.Mono);
            _enabled = true;
        }
        catch (Exception)
        {
            // No audio device (e.g. headless CI): run silently.
            _enabled = false;
        }
    }

    public float Volume => VolumeSteps[_volumeIndex];

    /// <summary>Cycles volume as the original's V key did. Returns the new level 0..3 (0 = mute).</summary>
    public int CycleVolume()
    {
        _volumeIndex = (_volumeIndex + 1) % VolumeSteps.Length;
        Play(Sfx.Shoot);
        return (int)Math.Round(Volume * 3);
    }

    public void Play(Sfx sfx, float pitch = 0f)
    {
        if (!_enabled || Volume <= 0f)
            return;
        if (_effects.TryGetValue(sfx, out var e))
        {
            try
            {
                e.Play(Volume, Math.Clamp(pitch, -1f, 1f), 0f);
            }
            catch (Exception)
            {
            }
        }
    }

    internal static IEnumerable<(Sfx, short[])> Build()
    {
        yield return (Sfx.Shoot, Synth.Sweep(0.12, 1900, 500, 0.45, 0.05));
        yield return (Sfx.EnemyHit, Synth.Mix(Synth.Noise(0.35, 0.7, 0, 3), Synth.Sweep(0.2, 400, 90, 0.3, 0)));
        yield return (Sfx.PlayerDeath, Synth.Mix(Synth.Noise(1.3, 0.8, 0, 6), Synth.Sweep(1.3, 220, 40, 0.35, 0)));
        yield return (Sfx.Bomb, Synth.Sweep(0.09, 700, 300, 0.25, 0.05));
        yield return (Sfx.Bonus, Synth.Tune(0.45, (523, 0.08), (659, 0.08), (784, 0.08), (1047, 0.2)));
        yield return (Sfx.Clone, Synth.Concat(Synth.Sweep(0.08, 300, 900, 0.4, 0.3), Synth.Sweep(0.08, 900, 300, 0.3, 0.1)));
        yield return (Sfx.Dive, Synth.Sweep(0.9, 2200, 300, 0.25, 0.15));
        yield return (Sfx.Refuel, Synth.Sweep(0.6, 200, 900, 0.35, 0.35));
        yield return (Sfx.Docked, Synth.Tune(0.45, (784, 0.07), (988, 0.07), (1175, 0.07), (1568, 0.25)));
        yield return (Sfx.Jump, Synth.Sweep(0.18, 300, 1100, 0.4, 0.1));
        yield return (Sfx.Land, Synth.Noise(0.06, 0.4, 0, 8));
        yield return (Sfx.Fall, Synth.Sweep(1.1, 1500, 120, 0.35, 0.2));
        yield return (Sfx.Step, Synth.Noise(0.02, 0.25, 0, 12));
        yield return (Sfx.Pickup, Synth.Tune(0.45, (1047, 0.06), (1319, 0.06), (1568, 0.06), (2093, 0.18)));
        yield return (Sfx.Bridge, Synth.Mix(Synth.Noise(0.25, 0.6, 0, 10), Synth.Sweep(0.25, 160, 60, 0.4, 0)));
        yield return (Sfx.Zap, Synth.Mix(Synth.Noise(0.4, 0.4, 0.1, 1), Synth.Sweep(0.4, 2500, 1800, 0.3, 0.1)));
        yield return (Sfx.Lift, Synth.Sweep(0.25, 120, 140, 0.2, 0.2));
        yield return (Sfx.Start, Synth.Tune(0.4, (392, 0.1), (523, 0.1), (659, 0.1), (784, 0.1), (659, 0.1), (784, 0.3)));
        yield return (Sfx.Warning, Synth.Tune(0.35, (880, 0.08), (0, 0.06), (880, 0.08)));
        yield return (Sfx.Victory, Synth.Tune(0.45,
            (523, 0.12), (523, 0.12), (523, 0.12), (659, 0.3), (587, 0.12), (659, 0.12), (784, 0.5)));
        yield return (Sfx.Rope, Synth.Sweep(0.1, 600, 500, 0.2, 0.1));
    }

    public void Dispose()
    {
        foreach (var e in _effects.Values)
            e.Dispose();
        _effects.Clear();
    }
}
