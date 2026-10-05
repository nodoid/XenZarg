using System;
using System.Collections.Generic;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Graphics;

/// <summary>
/// An immutable bitmap made of Oric colours, parsed from a character map
/// (see <see cref="OricPalette.FromCode"/>). Index 0 in <see cref="Pixels"/> is transparent,
/// otherwise the value is OricColor + 1.
/// </summary>
public sealed class Sprite
{
    private static readonly Dictionary<string[], Sprite> Cache = new(ReferenceEqualityComparer.Instance);

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public string Name { get; }

    public Sprite(string name, string[] rows)
    {
        if (rows == null || rows.Length == 0)
            throw new ArgumentException("Sprite needs at least one row", nameof(rows));
        Name = name;
        Height = rows.Length;
        foreach (var r in rows)
            Width = Math.Max(Width, r.Length);
        Pixels = new byte[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                var c = OricPalette.FromCode(row[x]);
                if (c.HasValue)
                    Pixels[y * Width + x] = (byte)((int)c.Value + 1);
            }
        }
    }

    private Sprite(string name, int w, int h, byte[] pixels)
    {
        Name = name;
        Width = w;
        Height = h;
        Pixels = pixels;
    }

    /// <summary>Returns the shared sprite for a static map (parsed once).</summary>
    public static Sprite Of(string[] rows, string name = null)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(rows, out var s))
            {
                s = new Sprite(name ?? "sprite", rows);
                Cache[rows] = s;
            }
            return s;
        }
    }

    public bool IsSolid(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height && Pixels[y * Width + x] != 0;

    public OricColor? ColorAt(int x, int y) =>
        IsSolid(x, y) ? (OricColor)(Pixels[y * Width + x] - 1) : null;

    private Sprite _mirror;
    private readonly Sprite[] _recolored = new Sprite[8];

    /// <summary>Horizontally mirrored copy (cached).</summary>
    public Sprite Mirror() => _mirror ??= BuildMirror();

    /// <summary>Copy with every solid pixel replaced by one colour (cached).</summary>
    public Sprite Recolor(OricColor color) => _recolored[(int)color & 7] ??= BuildRecolor(color);

    /// <summary>Copy enlarged by whole-pixel factors (new instance; cache it).</summary>
    public Sprite Scale(int sx, int sy)
    {
        int w = Width * sx, h = Height * sy;
        var p = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                p[y * w + x] = Pixels[(y / sy) * Width + x / sx];
        return new Sprite($"{Name}x{sx}x{sy}", w, h, p);
    }

    private Sprite BuildMirror()
    {
        var p = new byte[Pixels.Length];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                p[y * Width + x] = Pixels[y * Width + (Width - 1 - x)];
        return new Sprite(Name + "-mirror", Width, Height, p);
    }

    private Sprite BuildRecolor(OricColor color)
    {
        var p = new byte[Pixels.Length];
        for (int i = 0; i < p.Length; i++)
            p[i] = Pixels[i] == 0 ? (byte)0 : (byte)((int)color + 1);
        return new Sprite(Name + "-" + color, Width, Height, p);
    }

    /// <summary>
    /// Pixel-perfect overlap test between two sprites positioned at integer coordinates.
    /// </summary>
    public static bool Overlaps(Sprite a, int ax, int ay, Sprite b, int bx, int by)
    {
        int left = Math.Max(ax, bx);
        int top = Math.Max(ay, by);
        int right = Math.Min(ax + a.Width, bx + b.Width);
        int bottom = Math.Min(ay + a.Height, by + b.Height);
        if (left >= right || top >= bottom)
            return false;
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (a.IsSolid(x - ax, y - ay) && b.IsSolid(x - bx, y - by))
                    return true;
        return false;
    }

    /// <summary>Overlap test between a sprite and an axis-aligned rectangle.</summary>
    public static bool OverlapsRect(Sprite a, int ax, int ay, int rx, int ry, int rw, int rh)
    {
        int left = Math.Max(ax, rx);
        int top = Math.Max(ay, ry);
        int right = Math.Min(ax + a.Width, rx + rw);
        int bottom = Math.Min(ay + a.Height, ry + rh);
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (a.IsSolid(x - ax, y - ay))
                    return true;
        return false;
    }
}
