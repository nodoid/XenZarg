using System;
using Microsoft.Xna.Framework;
using XenZarg.Core;
using XenZarg.Core.Audio;
using XenZarg.Core.Graphics;
using XenZarg.Core.Input;
using XenZarg.Core.Oric;
using XenZarg.Core.Scenes;

namespace XenZarg.Tests;

public class SynthTests
{
    [Fact]
    public void ClipsHaveTheRequestedLength()
    {
        Assert.Equal(Synth.SampleRate / 2, Synth.Sweep(0.5, 100, 200).Length);
        Assert.Equal(Synth.SampleRate, Synth.Noise(1.0).Length);
        Assert.Equal((int)(0.1 * Synth.SampleRate) * 2, Synth.Tune(0.5, (440, 0.1), (0, 0.1)).Length);
    }

    [Fact]
    public void MixClipsInsteadOfOverflowing()
    {
        short[] loud = [short.MaxValue, short.MinValue];
        var mixed = Synth.Mix(loud, loud);
        Assert.Equal(short.MaxValue, mixed[0]);
        Assert.Equal(short.MinValue, mixed[1]);
    }

    [Fact]
    public void BytesAreLittleEndian16Bit()
    {
        var bytes = Synth.ToBytes([0x1234]);
        Assert.Equal(new byte[] { 0x34, 0x12 }, bytes);
    }

    [Fact]
    public void EverySoundEffectIsBuilt()
    {
        var built = new System.Collections.Generic.HashSet<Sfx>();
        foreach (var (id, pcm) in SoundBank.Build())
        {
            Assert.NotEmpty(pcm);
            built.Add(id);
        }
        Assert.Equal(Enum.GetValues<Sfx>().Length, built.Count);
    }
}

public class TiltMapperTests
{
    [Fact]
    public void DeadZoneIgnoresSmallTilts()
    {
        var m = new TiltMapper();
        Assert.Equal(0f, m.Axis(0.03f));
        Assert.Equal(0f, m.Axis(-0.05f));
    }

    [Fact]
    public void FullScaleClampsToOne()
    {
        var m = new TiltMapper();
        Assert.Equal(1f, m.Axis(0.9f));
        Assert.Equal(-1f, m.Axis(-0.9f));
        Assert.InRange(m.Axis(0.2f), 0.4f, 0.6f);
    }

    [Fact]
    public void CalibrationMakesTheHeldAngleNeutral()
    {
        var m = new TiltMapper { Smoothing = 0f };
        m.Map(new Vector2(0f, 0.5f));
        m.Calibrate();
        Assert.Equal(0f, m.Map(new Vector2(0f, 0.5f)).Y);
        Assert.Equal(1f, m.Map(new Vector2(0f, 0.9f)).Y);
    }
}

public class LayoutTests
{
    [Theory]
    [InlineData(960, 896, true, 0, 0, 960, 896)]
    [InlineData(1000, 900, true, 20, 2, 960, 896)]
    [InlineData(2400, 1080, false, 621, 0, 1157, 1080)]
    public void OricScreenIsLetterboxed(int w, int h, bool integer, int x, int y, int dw, int dh)
    {
        var r = XenZargGame.ComputeDestination(w, h, integer);
        Assert.Equal(new Rectangle(x, y, dw, dh), r);
    }

    private static void AssertPageFits(PageLine[] page)
    {
        foreach (var line in page)
        {
            Assert.True(line.Column + line.Text.Length <= Canvas.Columns, $"'{line.Text}' runs off the screen");
            Assert.InRange(line.Y, 0, Canvas.Height - OricFont.GlyphHeight);
            Assert.True(line.Y + OricFont.GlyphHeight <= 216, $"'{line.Text}' collides with the prompt");
        }
    }

    [Fact]
    public void InstructionPagesFitTheOricScreen()
    {
        AssertPageFits(InstructionPages.Xenon());
        AssertPageFits(InstructionPages.Zorgon());
        AssertPageFits(InstructionPages.Controls(true));
        AssertPageFits(InstructionPages.Controls(false));
    }

    [Fact]
    public void CreditsAreShown()
    {
        Assert.Equal("BY PFJ", XenZargGame.Credit);
        Assert.Equal("BASED ON THE PSS ORIGINALS", XenZargGame.BasedOn);
    }
}
