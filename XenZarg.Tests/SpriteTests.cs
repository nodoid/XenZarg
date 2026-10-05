using System.Linq;
using System.Reflection;
using XenZarg.Core.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Tests;

public class SpriteTests
{
    private static readonly string[] Cross =
    [
        ".R.",
        "RGR",
        ".R.",
    ];

    [Fact]
    public void ParsesSizeColoursAndTransparency()
    {
        var s = new Sprite("cross", Cross);
        Assert.Equal(3, s.Width);
        Assert.Equal(3, s.Height);
        Assert.False(s.IsSolid(0, 0));
        Assert.Equal(OricColor.Green, s.ColorAt(1, 1));
        Assert.Equal(OricColor.Red, s.ColorAt(1, 0));
        Assert.Null(s.ColorAt(5, 5));
    }

    [Fact]
    public void MirrorAndScale()
    {
        var s = new Sprite("l", ["RG"]);
        var m = s.Mirror();
        Assert.Equal(OricColor.Green, m.ColorAt(0, 0));
        Assert.Same(m, s.Mirror());

        var big = s.Scale(3, 2);
        Assert.Equal(6, big.Width);
        Assert.Equal(2, big.Height);
        Assert.Equal(OricColor.Red, big.ColorAt(2, 1));
        Assert.Equal(OricColor.Green, big.ColorAt(3, 0));
    }

    [Fact]
    public void RecolorKeepsShape()
    {
        var s = new Sprite("cross", Cross).Recolor(OricColor.White);
        Assert.Equal(OricColor.White, s.ColorAt(1, 1));
        Assert.False(s.IsSolid(0, 0));
    }

    [Fact]
    public void OverlapIsPixelPerfect()
    {
        var a = new Sprite("cross", Cross);
        // Boxes overlap only at transparent corners.
        Assert.False(Sprite.Overlaps(a, 0, 0, a, 2, 2));
        Assert.True(Sprite.Overlaps(a, 0, 0, a, 1, 1));
        Assert.False(Sprite.Overlaps(a, 0, 0, a, 10, 10));
        Assert.True(Sprite.OverlapsRect(a, 0, 0, 1, 1, 1, 1));
        Assert.False(Sprite.OverlapsRect(a, 0, 0, 0, 0, 1, 1));
    }

    public static TheoryData<string> AllSprites()
    {
        var data = new TheoryData<string>();
        foreach (var f in typeof(SpriteData).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.FieldType == typeof(string[]))
                data.Add(f.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllSprites))]
    public void CapturedSpritesUseOnlyOricColoursAndAreRectangular(string name)
    {
        var rows = (string[])typeof(SpriteData).GetField(name)!.GetValue(null)!;
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(rows[0].Length, r.Length));
        Assert.All(rows, r => Assert.True(r.All(ch => ".KRGYBMCW".Contains(ch)), $"{name}: bad code in '{r}'"));
        var sprite = Sprite.Of(rows, name);
        Assert.Contains(sprite.Pixels, p => p != 0);
    }

    [Fact]
    public void ShipColoursMatchTheOriginal()
    {
        // Captured from the Oric: white nose, red cockpit, yellow body, magenta exhaust.
        var ship = Sprite.Of(SpriteData.PlayerShip);
        Assert.Equal(OricColor.White, ship.ColorAt(5, 0));
        Assert.Equal(OricColor.Red, ship.ColorAt(5, 4));
        Assert.Equal(OricColor.Yellow, ship.ColorAt(5, 9));
        Assert.Equal(OricColor.Magenta, ship.ColorAt(5, 15));
    }
}
