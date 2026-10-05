using Microsoft.Xna.Framework;

namespace XenZarg.Core.Oric;

/// <summary>The eight fixed colours of the Oric ULA, in attribute order.</summary>
public enum OricColor : byte
{
    Black = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Magenta = 5,
    Cyan = 6,
    White = 7,
}

public static class OricPalette
{
    /// <summary>Pure RGB primaries exactly as the Oric outputs them.</summary>
    public static readonly Color[] Colors =
    [
        new Color(0, 0, 0),
        new Color(255, 0, 0),
        new Color(0, 255, 0),
        new Color(255, 255, 0),
        new Color(0, 0, 255),
        new Color(255, 0, 255),
        new Color(0, 255, 255),
        new Color(255, 255, 255),
    ];

    public static Color ToColor(this OricColor c) => Colors[(int)c & 7];

    /// <summary>Oric inverse video: colour index XOR 7.</summary>
    public static OricColor Inverse(this OricColor c) => (OricColor)((int)c ^ 7);

    /// <summary>
    /// Maps a sprite map character to a colour. '.' and ' ' are transparent (null).
    /// K black, R red, G green, Y yellow, B blue, M magenta, C cyan, W white.
    /// </summary>
    public static OricColor? FromCode(char code) => code switch
    {
        'K' => OricColor.Black,
        'R' => OricColor.Red,
        'G' => OricColor.Green,
        'Y' => OricColor.Yellow,
        'B' => OricColor.Blue,
        'M' => OricColor.Magenta,
        'C' => OricColor.Cyan,
        'W' => OricColor.White,
        _ => null,
    };

    public static char ToCode(OricColor c) => "KRGYBMCW"[(int)c & 7];
}
