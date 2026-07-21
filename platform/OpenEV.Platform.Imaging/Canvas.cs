using System;

namespace OpenEV.Platform.Imaging;

// Pure-C# software rasterizer over an Rgba8Image. This is the managed analogue
// of the MonoGame SpriteBatch the draw closures used to call — the host seam
// (MacToolbox.EnqueueDraw) hands each command a Canvas bound to the current
// GWorld buffer instead of a SpriteBatch bound to a RenderTarget2D.
//
// Blend model (faithfulness): every primitive except InvertRect reproduces the
// XNA default BlendState.AlphaBlend the old code drew under — source factor One,
// dest factor InverseSourceAlpha — applied to the TINTED source texel:
//   out.rgb = ts.rgb + dst.rgb * (255 - ts.a) / 255
//   out.a   = ts.a   + dst.a   * (255 - ts.a) / 255
// For opaque pixels (ts.a == 255) this is a straight copy; for fully transparent
// sprite/mask pixels (ts.a == 0, and rgb == 0 in EVO's mask frames) it leaves the
// destination untouched. This matches what the GPU produced bit-for-bit on EVO's
// non-premultiplied PICT/sprite uploads, and makes the screen-fade tint
// (Color.White * f, alpha scaled too) blend the image toward the cleared
// FadeColor exactly as before.
//
// Sampling is nearest-neighbor (the old code drew under SamplerState.PointClamp).
// Every primitive clips to the bound target — callers (dialogs especially) draw
// at absolute / negative coords and relied on the GPU clipping for them.
//
// Alpha-byte provenance tag: opaque pixels carry HOW they were drawn in their
// stored alpha — 255 = copied from an image (CopyBits/DrawPicture/sprite blits:
// the Mac's INDEX-preserving path), 254 (RgbDrawnTag) = produced by a QuickDraw
// RGB primitive (PaintRect/pen/text/invert: the Mac's Color2Index inverse-table
// path). Both values are fully opaque to the blender (Blend treats a >= 254 as
// opaque and PRESERVES the tag through image blits, so window-layer composites
// and the offscreen->screen flush keep each pixel's provenance). The cloak
// screen-palette remap (MacToolbox.ApplyScreenPaletteRemap) dispatches on the
// tag: on the Mac an indexed pixel retints to ITS OWN entry's remapped colour
// while an RGB-drawn pixel resolves through the inverse table over the remapped
// entries — visibly different for saturated colours. Nothing else reads alpha.
public sealed class Canvas
{
    /// Stored-alpha value marking a pixel as QuickDraw-RGB-drawn (see class note).
    public const byte RgbDrawnTag = 254;
    /// The GWorld buffer subsequent primitives draw into. DrainDrawQueue swaps
    /// this when a command's render-target key changes (Mac GWorld semantics).
    public Rgba8Image? Target { get; set; }

    public Canvas() { }
    public Canvas(Rgba8Image target) { Target = target; }

    // ── Solid fill (PaintRect / EraseRect / dialog fills / FillCRect / the
    //    1×1 and 1×N rects the oval & round-rect closures decompose into) ──────
    public void FillRect(RectI r, RgbaColor c)
    {
        var t = Target; if (t is null) return;
        int x0 = Math.Max(0, r.Left),  y0 = Math.Max(0, r.Top);
        int x1 = Math.Min(t.Width,  r.Right), y1 = Math.Min(t.Height, r.Bottom);
        if (x1 <= x0 || y1 <= y0) return;
        byte[] px = t.Pixels; int stride = t.Width * 4;

        if (c.A == 255)
        {
            Span<int> pxInt = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(px.AsSpan());
            int colorVal = BitConverter.IsLittleEndian
                ? c.R | (c.G << 8) | (c.B << 16) | (RgbDrawnTag << 24)
                : (c.R << 24) | (c.G << 16) | (c.B << 8) | RgbDrawnTag;

            int width = t.Width;
            int count = x1 - x0;
            for (int y = y0; y < y1; y++)
            {
                int oInt = y * width + x0;
                pxInt.Slice(oInt, count).Fill(colorVal);
            }
            return;
        }
        if (c.A == 0) return;
        for (int y = y0; y < y1; y++)
        {
            int o = y * stride + x0 * 4;
            for (int x = x0; x < x1; x++) { Blend(px, o, c.R, c.G, c.B, c.A, RgbDrawnTag); o += 4; }
        }
    }

    /// Fill the whole bound target with one colour (host clear).
    public void Clear(RgbaColor c)
    {
        var t = Target; if (t is null) return;
        FillRect(new RectI(0, 0, t.Width, t.Height), c);
    }

    // ── Tiled pattern fill (FillCRect with a decoded ppat tile) ───────────────
    /// Tile `pattern` across `r` (phase-anchored to the rect's top-left), over
    /// the bound target. Patterns are opaque, so this is a straight copy.
    public void FillPattern(RectI r, Rgba8Image pattern)
    {
        var t = Target; if (t is null || pattern is null) return;
        int pw = pattern.Width, ph = pattern.Height;
        if (pw <= 0 || ph <= 0) return;
        int x0 = Math.Max(0, r.Left),  y0 = Math.Max(0, r.Top);
        int x1 = Math.Min(t.Width,  r.Right), y1 = Math.Min(t.Height, r.Bottom);
        if (x1 <= x0 || y1 <= y0) return;
        byte[] dp = t.Pixels;
        byte[] sp = pattern.Pixels;

        Span<int> dpInt = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(dp.AsSpan());
        Span<int> spInt = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(sp.AsSpan());

        int tWidth = t.Width;
        bool isLittleEndian = BitConverter.IsLittleEndian;

        for (int y = y0; y < y1; y++)
        {
            int sy = (y - r.Top) % ph; // y - r.Top is non-negative since y >= y0 >= r.Top
            int dRow = y * tWidth, sRow = sy * pw;
            for (int x = x0; x < x1; x++)
            {
                int sx = (x - r.Left) % pw; // x - r.Left is non-negative since x >= x0 >= r.Left
                int pixel = spInt[sRow + sx];
                if (isLittleEndian)
                {
                    dpInt[dRow + x] = (pixel & 0x00FFFFFF) | (255 << 24);
                }
                else
                {
                    dpInt[dRow + x] = (pixel & unchecked((int)0xFFFFFF00)) | 255;
                }
            }
        }
    }

    // ── Textured blit (CopyBits / DrawPicture / sprite & orb blits / the
    //    _gameTarget→_virtualTarget flush / the fade composite) ────────────────
    /// Blit `src[srcRect]` into `dst` with nearest-neighbor scaling and a colour
    /// tint, over the bound target.
    public void Blit(Rgba8Image src, RectI dst, RectI srcRect, RgbaColor tint)
        => BlitCore(src, dst, srcRect, tint, rgbTag: false);

    private void BlitCore(Rgba8Image src, RectI dst, RectI srcRect, RgbaColor tint, bool rgbTag)
    {
        var t = Target; if (t is null || src is null) return;
        if (dst.Width <= 0 || dst.Height <= 0 || srcRect.Width <= 0 || srcRect.Height <= 0) return;

        int dx0 = Math.Max(0, dst.Left),  dy0 = Math.Max(0, dst.Top);
        int dx1 = Math.Min(t.Width,  dst.Right), dy1 = Math.Min(t.Height, dst.Bottom);
        if (dx1 <= dx0 || dy1 <= dy0) return;

        byte[] dp = t.Pixels;     int dStride = t.Width * 4;
        byte[] sp = src.Pixels;   int sStride = src.Width * 4;
        bool noTint = tint.IsOpaqueWhite;
        int sxMax = srcRect.Right - 1, syMax = srcRect.Bottom - 1;

        int xCount = dx1 - dx0;
        int[]? sxArray = null;
        Span<int> sxLookup = xCount <= 1024 
            ? stackalloc int[xCount] 
            : (sxArray = System.Buffers.ArrayPool<int>.Shared.Rent(xCount));

        int yCount = dy1 - dy0;
        int[]? syArray = null;
        Span<int> sRowLookup = yCount <= 1024 
            ? stackalloc int[yCount] 
            : (syArray = System.Buffers.ArrayPool<int>.Shared.Rent(yCount));

        try
        {
            for (int i = 0; i < xCount; i++)
            {
                int x = dx0 + i;
                int sx = srcRect.X + (int)((long)(x - dst.X) * srcRect.Width / dst.Width);
                if (sx < srcRect.X) sx = srcRect.X; else if (sx > sxMax) sx = sxMax;
                if (sx < 0) sx = 0; else if (sx >= src.Width) sx = src.Width - 1;
                sxLookup[i] = sx * 4;
            }

            for (int j = 0; j < yCount; j++)
            {
                int y = dy0 + j;
                int sy = srcRect.Y + (int)((long)(y - dst.Y) * srcRect.Height / dst.Height);
                if (sy < srcRect.Y) sy = srcRect.Y; else if (sy > syMax) sy = syMax;
                if (sy < 0) sy = 0; else if (sy >= src.Height) sy = src.Height - 1;
                sRowLookup[j] = sy * sStride;
            }

            for (int j = 0; j < yCount; j++)
            {
                int y = dy0 + j;
                int sRow = sRowLookup[j];
                int dRow = y * dStride;

                for (int i = 0; i < xCount; i++)
                {
                    int so = sRow + sxLookup[i];
                    int sr = sp[so], sg = sp[so + 1], sb = sp[so + 2], sa = sp[so + 3];
                    if (!noTint)
                    {
                        sr = sr * tint.R / 255; sg = sg * tint.G / 255;
                        sb = sb * tint.B / 255; sa = sa * tint.A / 255;
                    }
                    Blend(dp, dRow + (dx0 + i) * 4, sr, sg, sb, sa, rgbTag ? RgbDrawnTag : (byte)sa);
                }
            }
        }
        finally
        {
            if (sxArray is not null) System.Buffers.ArrayPool<int>.Shared.Return(sxArray);
            if (syArray is not null) System.Buffers.ArrayPool<int>.Shared.Return(syArray);
        }
    }

    /// Blit the whole of `src` into `dst` (no source sub-rect).
    public void Blit(Rgba8Image src, RectI dst, RgbaColor tint)
        => Blit(src, dst, new RectI(0, 0, src.Width, src.Height), tint);

    /// Glyph-quad sink for the software FontStashSharp renderer. Same op as Blit
    /// (textured, tinted, over) — named for intent at the call site, and stamps
    /// the QuickDraw-RGB provenance tag (text is an RGB draw on the Mac).
    public void BlitGlyph(Rgba8Image atlas, RectI dst, RectI srcRect, RgbaColor tint)
        => BlitCore(atlas, dst, srcRect, tint, rgbTag: true);

    // ── XOR invert (InvertRect — selection highlight, key-rebind capture) ──────
    // Reproduces the old InvertBlend (InverseDestinationColor / Zero colour, One /
    // Zero alpha): dst.rgb = 255 - dst.rgb, dst.a forced opaque. Self-inverse.
    public void InvertRect(RectI r)
    {
        var t = Target; if (t is null) return;
        int x0 = Math.Max(0, r.Left),  y0 = Math.Max(0, r.Top);
        int x1 = Math.Min(t.Width,  r.Right), y1 = Math.Min(t.Height, r.Bottom);
        if (x1 <= x0 || y1 <= y0) return;
        byte[] px = t.Pixels;
        Span<int> pxInt = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(px.AsSpan());

        int width = t.Width;
        if (BitConverter.IsLittleEndian)
        {
            int tagShifted = RgbDrawnTag << 24;
            for (int y = y0; y < y1; y++)
            {
                int oInt = y * width + x0;
                for (int x = x0; x < x1; x++)
                {
                    pxInt[oInt] = (~pxInt[oInt] & 0x00FFFFFF) | tagShifted;
                    oInt++;
                }
            }
        }
        else
        {
            for (int y = y0; y < y1; y++)
            {
                int oInt = y * width + x0;
                for (int x = x0; x < x1; x++)
                {
                    pxInt[oInt] = (~pxInt[oInt] & unchecked((int)0xFFFFFF00)) | RgbDrawnTag;
                    oInt++;
                }
            }
        }
    }

    // ── Bit scroll (ScrollRect — galaxy-map drag-pan) ─────────────────────────
    /// QuickDraw ScrollRect — shift the pixels inside `r` of the bound target by
    /// (dh, dv) (positive dh = right, dv = down); bits pushed outside `r` are lost,
    /// and the strip `r` vacates is filled with `fill`. Snapshots the region first,
    /// so the in-place move is safe when source and destination overlap (a plain
    /// self-Blit would read pixels this same pass already overwrote). Each moved
    /// pixel is copied verbatim, stored alpha/provenance byte included — the Mac
    /// scrolls indexed pixels without re-resolving them.
    public void ScrollRect(RectI r, int dh, int dv, RgbaColor fill)
    {
        var t = Target; if (t is null) return;
        if (dh == 0 && dv == 0) return;                        // Mac: zero shift does nothing (no fill)
        int x0 = Math.Max(0, r.Left),  y0 = Math.Max(0, r.Top);
        int x1 = Math.Min(t.Width, r.Right), y1 = Math.Min(t.Height, r.Bottom);
        if (x1 <= x0 || y1 <= y0) return;
        int w = x1 - x0, h = y1 - y0;
        byte[] px = t.Pixels;
        int stride = t.Width * 4;

        // Snapshot the (clipped) source region so the shifted write-back can't read
        // cells it has already overwritten when source and destination overlap.
        byte[] snap = new byte[w * h * 4];
        for (int sy = 0; sy < h; sy++)
            Array.Copy(px, (y0 + sy) * stride + x0 * 4, snap, sy * w * 4, w * 4);

        // Paint the whole rect the vacated colour, then lay the snapshot back
        // shifted — the shift re-covers everything except the (dh, dv) strip it opens.
        FillRect(new RectI(x0, y0, w, h), fill);

        for (int sy = 0; sy < h; sy++)
        {
            int dyPix = y0 + sy + dv;
            if (dyPix < y0 || dyPix >= y1) continue;           // row scrolled out of r
            int srcRow = sy * w * 4;
            int dstRow = dyPix * stride;
            for (int sx = 0; sx < w; sx++)
            {
                int dxPix = x0 + sx + dh;
                if (dxPix < x0 || dxPix >= x1) continue;       // column scrolled out of r
                int so = srcRow + sx * 4;
                int dOff = dstRow + dxPix * 4;
                px[dOff] = snap[so]; px[dOff + 1] = snap[so + 1];
                px[dOff + 2] = snap[so + 2]; px[dOff + 3] = snap[so + 3];
            }
        }
    }

    // ── Pen stroke (Line / LineTo — radar, nav arrow, beams, hyperspace lanes,
    //    galaxy-map route & selection brackets, outfitter box borders) ──────────
    /// Stroke from (x0,y0) to (x1,y1) with a pen of size penW×penH. A zero-length
    /// segment paints the single penW×penH pen rect (the radar blip dot).
    /// QuickDraw's pen hangs below-right of the pen location, so a thick line is
    /// the union of penW×penH rects stamped top-left-anchored at every pixel of
    /// the 1px path. (The previous approximation laid penH parallel 1px lines
    /// along the perpendicular, which shifted the band off the pen's true
    /// down-right hang and left notches on diagonals.)
    public void StrokeLine(int x0, int y0, int x1, int y1, int penW, int penH, RgbaColor c)
    {
        if (Target is null) return;
        penW = Math.Max(1, penW); penH = Math.Max(1, penH);
        int dx = x1 - x0, dy = y1 - y0;
        if (dx == 0 && dy == 0) { FillRect(new RectI(x0, y0, penW, penH), c); return; }

        if (penW == 1 && penH == 1) { DrawThinLine(x0, y0, x1, y1, c); return; }

        // Stamp the pen rect at each path pixel. Fully-opaque pens overwrite, so
        // the overlap between consecutive stamps is idempotent; translucent pens
        // aren't used by any stroke caller.
        int adx = Math.Abs(dx), sx = x0 < x1 ? 1 : -1;
        int ady = -Math.Abs(dy), sy = y0 < y1 ? 1 : -1;
        int err = adx + ady;
        while (true)
        {
            FillRect(new RectI(x0, y0, penW, penH), c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= ady) { err += ady; x0 += sx; }
            if (e2 <= adx) { err += adx; y0 += sy; }
        }
    }

    private void DrawThinLine(int x0, int y0, int x1, int y1, RgbaColor c)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Plot(x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Plot(int x, int y, RgbaColor c)
    {
        var t = Target!;
        if ((uint)x >= (uint)t.Width || (uint)y >= (uint)t.Height) return;
        Blend(t.Pixels, (y * t.Width + x) * 4, c.R, c.G, c.B, c.A, RgbDrawnTag);
    }

    // XNA premultiplied AlphaBlend of a (tinted) source over dst at byte offset o.
    // `opaqueStamp` is the stored alpha for the opaque case (the provenance tag);
    // alpha >= RgbDrawnTag (254) counts as opaque so tagged pixels re-blit exactly.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Blend(byte[] px, int o, int sr, int sg, int sb, int sa, byte opaqueStamp)
    {
        if (sa <= 0) return;
        if (sa >= RgbDrawnTag)
        {
            px[o] = (byte)sr; px[o + 1] = (byte)sg; px[o + 2] = (byte)sb; px[o + 3] = opaqueStamp;
            return;
        }
        int inv = 255 - sa;
        int r = sr + px[o]     * inv / 255;
        int g = sg + px[o + 1] * inv / 255;
        int b = sb + px[o + 2] * inv / 255;
        int a = sa + px[o + 3] * inv / 255;
        px[o]     = r > 255 ? (byte)255 : (byte)r;
        px[o + 1] = g > 255 ? (byte)255 : (byte)g;
        px[o + 2] = b > 255 ? (byte)255 : (byte)b;
        px[o + 3] = a > 255 ? (byte)255 : (byte)a;
    }
}
