using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XenZarg.Core.Oric;

namespace XenZarg.Core.Graphics;

/// <summary>
/// Draws into the 240x224 virtual Oric screen: sprites, filled blocks and ROM-font text.
/// All coordinates are in Oric pixels.
/// </summary>
public sealed class Canvas : IDisposable
{
    public const int Width = 240;
    public const int Height = 224;
    public const int Columns = Width / OricFont.GlyphWidth;   // 40
    public const int Rows = Height / OricFont.GlyphHeight;    // 28

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;
    private readonly Texture2D _font;
    private readonly Dictionary<Sprite, Texture2D> _textures = new();

    public Canvas(GraphicsDevice device)
    {
        _device = device;
        _batch = new SpriteBatch(device);
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);
        _font = BuildFont(device);
    }

    public SpriteBatch Batch => _batch;

    public void Begin() => _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

    public void End() => _batch.End();

    public void Fill(int x, int y, int w, int h, OricColor color)
    {
        if (w <= 0 || h <= 0)
            return;
        _batch.Draw(_pixel, new Rectangle(x, y, w, h), color.ToColor());
    }

    public void Plot(int x, int y, OricColor color) => Fill(x, y, 1, 1, color);

    public void Draw(Sprite sprite, int x, int y)
    {
        _batch.Draw(TextureFor(sprite), new Vector2(x, y), Color.White);
    }

    public void Draw(string[] map, int x, int y) => Draw(Sprite.Of(map), x, y);

    /// <summary>Draws a sprite with every solid pixel in a single colour.</summary>
    public void DrawTinted(Sprite sprite, int x, int y, OricColor color)
    {
        _batch.Draw(TextureFor(sprite.Recolor(OricColor.White)), new Vector2(x, y), color.ToColor());
    }

    /// <summary>Text at a pixel position, transparent paper.</summary>
    public void Text(int x, int y, string text, OricColor ink)
    {
        var tint = ink.ToColor();
        for (int i = 0; i < text.Length; i++)
        {
            int index = text[i] - OricFont.FirstChar;
            if (index <= 0 || index >= OricFont.CharCount)
                continue;
            var src = new Rectangle(index * OricFont.GlyphWidth, 0, OricFont.GlyphWidth, OricFont.GlyphHeight);
            _batch.Draw(_font, new Vector2(x + i * OricFont.GlyphWidth, y), src, tint);
        }
    }

    /// <summary>Text at a character cell (40 columns).</summary>
    public void TextAt(int column, int y, string text, OricColor ink) =>
        Text(column * OricFont.GlyphWidth, y, text, ink);

    /// <summary>Text horizontally centred on the 40 column screen.</summary>
    public void TextCentered(int y, string text, OricColor ink) =>
        Text((Width - text.Length * OricFont.GlyphWidth) / 2, y, text, ink);

    private Texture2D TextureFor(Sprite sprite)
    {
        if (_textures.TryGetValue(sprite, out var tex))
            return tex;
        var data = new Color[sprite.Width * sprite.Height];
        for (int i = 0; i < data.Length; i++)
        {
            byte p = sprite.Pixels[i];
            data[i] = p == 0 ? Color.Transparent : OricPalette.Colors[p - 1];
        }
        tex = new Texture2D(_device, sprite.Width, sprite.Height);
        tex.SetData(data);
        _textures[sprite] = tex;
        return tex;
    }

    private static Texture2D BuildFont(GraphicsDevice device)
    {
        int w = OricFont.CharCount * OricFont.GlyphWidth;
        int h = OricFont.GlyphHeight;
        var data = new Color[w * h];
        for (int ch = 0; ch < OricFont.CharCount; ch++)
        {
            char c = (char)(OricFont.FirstChar + ch);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < OricFont.GlyphWidth; x++)
                    if (OricFont.IsSet(c, x, y))
                        data[y * w + ch * OricFont.GlyphWidth + x] = Color.White;
        }
        var tex = new Texture2D(device, w, h);
        tex.SetData(data);
        return tex;
    }

    public void Dispose()
    {
        foreach (var t in _textures.Values)
            t.Dispose();
        _textures.Clear();
        _pixel.Dispose();
        _font.Dispose();
        _batch.Dispose();
    }
}
