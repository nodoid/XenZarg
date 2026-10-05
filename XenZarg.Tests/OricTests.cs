using XenZarg.Core.Oric;

namespace XenZarg.Tests;

public class OricTests
{
    [Fact]
    public void PaletteHasTheEightPureOricColours()
    {
        Assert.Equal(8, OricPalette.Colors.Length);
        Assert.Equal(new Microsoft.Xna.Framework.Color(255, 0, 255), OricColor.Magenta.ToColor());
        Assert.Equal(new Microsoft.Xna.Framework.Color(0, 0, 0), OricColor.Black.ToColor());
        foreach (var c in OricPalette.Colors)
        {
            Assert.True(c.R is 0 or 255 && c.G is 0 or 255 && c.B is 0 or 255);
        }
    }

    [Theory]
    [InlineData(OricColor.Black, OricColor.White)]
    [InlineData(OricColor.Red, OricColor.Cyan)]
    [InlineData(OricColor.Yellow, OricColor.Blue)]
    public void InverseVideoXorsWithSeven(OricColor c, OricColor expected) => Assert.Equal(expected, c.Inverse());

    [Fact]
    public void ColourCodesRoundTrip()
    {
        for (int i = 0; i < 8; i++)
        {
            var c = (OricColor)i;
            Assert.Equal(c, OricPalette.FromCode(OricPalette.ToCode(c)));
        }
        Assert.Null(OricPalette.FromCode('.'));
        Assert.Null(OricPalette.FromCode(' '));
    }

    [Fact]
    public void FontMatchesOricRomLetterA()
    {
        // ROM rows for 'A': 08 14 22 22 3E 22 22 00
        byte[] expected = [0x08, 0x14, 0x22, 0x22, 0x3E, 0x22, 0x22, 0x00];
        Assert.Equal(expected, OricFont.Glyph('A').ToArray());
        Assert.True(OricFont.IsSet('A', 2, 0));
        Assert.False(OricFont.IsSet('A', 0, 0));
    }

    [Fact]
    public void FontHandlesOutOfRangeCharacters()
    {
        Assert.Equal(new byte[8], OricFont.Glyph('☃').ToArray());
        Assert.False(OricFont.IsSet('A', -1, 0));
        Assert.False(OricFont.IsSet('A', 6, 0));
        Assert.False(OricFont.IsSet('A', 0, 8));
    }

    [Fact]
    public void EveryGlyphFitsInSixPixels()
    {
        for (char c = ' '; c <= '\u007f'; c++)
            foreach (byte row in OricFont.Glyph(c))
                Assert.True(row < 64, $"glyph {(int)c} uses more than 6 bits");
    }
}
