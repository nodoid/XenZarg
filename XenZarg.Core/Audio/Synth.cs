using System;

namespace XenZarg.Core.Audio;

/// <summary>
/// Tiny PCM generator imitating the Oric's AY-3-8912: square-wave tones,
/// a noise channel and simple linear envelopes. Output is 16-bit mono.
/// </summary>
public static class Synth
{
    public const int SampleRate = 22050;

    public static short[] Silence(double seconds) => new short[(int)(seconds * SampleRate)];

    /// <summary>Square wave sweeping from f0 to f1 Hz with a linear volume envelope.</summary>
    public static short[] Sweep(double seconds, double f0, double f1, double v0 = 0.6, double v1 = 0.0)
    {
        int n = Math.Max(1, (int)(seconds * SampleRate));
        var s = new short[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double f = f0 + (f1 - f0) * t;
            phase += f / SampleRate;
            double v = v0 + (v1 - v0) * t;
            s[i] = (short)((phase % 1.0 < 0.5 ? 1 : -1) * v * short.MaxValue * 0.5);
        }
        return s;
    }

    /// <summary>LFSR noise, optionally periodically held to lower its pitch.</summary>
    public static short[] Noise(double seconds, double v0 = 0.7, double v1 = 0.0, int hold = 2, int seed = 0x1ACE)
    {
        int n = Math.Max(1, (int)(seconds * SampleRate));
        var s = new short[n];
        uint lfsr = (uint)seed | 1;
        int bit = 1;
        for (int i = 0; i < n; i++)
        {
            if (i % Math.Max(1, hold) == 0)
            {
                bit = (int)(lfsr & 1);
                lfsr = (lfsr >> 1) ^ (uint)(-(int)(lfsr & 1) & 0x12000);
            }
            double t = (double)i / n;
            double v = v0 + (v1 - v0) * t;
            s[i] = (short)((bit == 1 ? 1 : -1) * v * short.MaxValue * 0.5);
        }
        return s;
    }

    /// <summary>A sequence of square-wave notes (Hz, seconds); 0 Hz is a rest.</summary>
    public static short[] Tune(double volume, params (double hz, double seconds)[] notes)
    {
        int total = 0;
        foreach (var (_, d) in notes)
            total += (int)(d * SampleRate);
        var s = new short[Math.Max(1, total)];
        int pos = 0;
        foreach (var (hz, d) in notes)
        {
            int n = (int)(d * SampleRate);
            double phase = 0;
            for (int i = 0; i < n && pos < s.Length; i++, pos++)
            {
                if (hz <= 0)
                    continue;
                phase += hz / SampleRate;
                double env = 1.0 - 0.6 * i / n;
                s[pos] = (short)((phase % 1.0 < 0.5 ? 1 : -1) * volume * env * short.MaxValue * 0.5);
            }
        }
        return s;
    }

    /// <summary>Sums clips sample by sample with clipping.</summary>
    public static short[] Mix(params short[][] clips)
    {
        int n = 0;
        foreach (var c in clips)
            n = Math.Max(n, c.Length);
        var s = new short[n];
        for (int i = 0; i < n; i++)
        {
            int sum = 0;
            foreach (var c in clips)
                if (i < c.Length)
                    sum += c[i];
            s[i] = (short)Math.Clamp(sum, short.MinValue, short.MaxValue);
        }
        return s;
    }

    public static short[] Concat(params short[][] clips)
    {
        int n = 0;
        foreach (var c in clips)
            n += c.Length;
        var s = new short[n];
        int pos = 0;
        foreach (var c in clips)
        {
            Array.Copy(c, 0, s, pos, c.Length);
            pos += c.Length;
        }
        return s;
    }

    public static byte[] ToBytes(short[] samples)
    {
        var b = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, b, 0, b.Length);
        if (!BitConverter.IsLittleEndian)
            for (int i = 0; i < b.Length; i += 2)
                (b[i], b[i + 1]) = (b[i + 1], b[i]);
        return b;
    }
}
