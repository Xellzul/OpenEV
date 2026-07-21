using System;
using System.Collections.Generic;

namespace OpenEV.Platform.Imaging;

// Maps an Rgba8Image's pixels onto a ≤256-colour indexed palette — the shared front end for the 8-bit
// indexed encoders (PICT 'PackBitsRect' and 'cicn'). A supplied palette (flat RGB triples, e.g. the
// game's master clut 1001) is used verbatim so the in-game 8-bit remap is an identity; otherwise a
// per-image palette is built by median cut.
internal static class PaletteQuantizer
{
    /// <summary>Choose a palette and map every pixel to its nearest index. Returns the palette and fills
    /// <paramref name="index"/> (one byte per pixel, row-major).</summary>
    public static (byte R, byte G, byte B)[] BuildIndexMap(Rgba8Image img, byte[]? palette, out byte[] index)
    {
        int w = img.Width, h = img.Height;
        var px = img.Pixels;

        (byte R, byte G, byte B)[] pal = palette is not null ? FromFlat(palette) : MedianCut(px, w * h, 256);
        if (pal.Length == 0) pal = new[] { ((byte)0, (byte)0, (byte)0) };
        if (pal.Length > 256) Array.Resize(ref pal, 256);

        var nearestCache = new Dictionary<int, byte>();
        index = new byte[w * h];
        for (int p = 0; p < w * h; p++)
        {
            int o = p * 4;
            int key = px[o] << 16 | px[o + 1] << 8 | px[o + 2];
            if (!nearestCache.TryGetValue(key, out byte idx))
            {
                idx = Nearest(px[o], px[o + 1], px[o + 2], pal);
                nearestCache[key] = idx;
            }
            index[p] = idx;
        }
        return pal;
    }

    private static (byte R, byte G, byte B)[] FromFlat(byte[] flat)
    {
        int n = flat.Length / 3;
        var pal = new (byte, byte, byte)[n];
        for (int i = 0; i < n; i++) pal[i] = (flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
        return pal;
    }

    private static byte Nearest(byte r, byte g, byte b, (byte R, byte G, byte B)[] pal)
    {
        int best = 0, bestD = int.MaxValue;
        for (int i = 0; i < pal.Length; i++)
        {
            int dr = r - pal[i].R, dg = g - pal[i].G, db = b - pal[i].B;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = i; if (d == 0) break; }
        }
        return (byte)best;
    }

    // Simple median-cut quantizer over the distinct colours of the image (fallback when no game palette).
    private static (byte R, byte G, byte B)[] MedianCut(byte[] rgba, int pixels, int maxColors)
    {
        var counts = new Dictionary<int, int>();
        for (int p = 0; p < pixels; p++)
        {
            int o = p * 4, key = rgba[o] << 16 | rgba[o + 1] << 8 | rgba[o + 2];
            counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
        }
        var colors = new List<int>(counts.Keys);
        if (colors.Count <= maxColors)
        {
            var exact = new (byte, byte, byte)[colors.Count];
            for (int i = 0; i < colors.Count; i++) exact[i] = ((byte)(colors[i] >> 16), (byte)(colors[i] >> 8), (byte)colors[i]);
            return exact;
        }
        var boxes = new List<List<int>> { colors };
        while (boxes.Count < maxColors)
        {
            List<int>? target = null; int targetIdx = -1;
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].Count > 1 && (target is null || boxes[i].Count > target.Count)) { target = boxes[i]; targetIdx = i; }
            if (target is null) break;
            int ch = WidestChannel(target);
            target.Sort((a, b) => Chan(a, ch) - Chan(b, ch));
            int mid = target.Count / 2;
            boxes[targetIdx] = target.GetRange(0, mid);
            boxes.Add(target.GetRange(mid, target.Count - mid));
        }
        var pal = new (byte, byte, byte)[boxes.Count];
        for (int i = 0; i < boxes.Count; i++)
        {
            long sr = 0, sg = 0, sb = 0, tot = 0;
            foreach (int key in boxes[i]) { int wt = counts[key]; sr += (key >> 16 & 0xff) * wt; sg += (key >> 8 & 0xff) * wt; sb += (key & 0xff) * wt; tot += wt; }
            tot = tot == 0 ? 1 : tot;
            pal[i] = ((byte)(sr / tot), (byte)(sg / tot), (byte)(sb / tot));
        }
        return pal;
    }

    private static int Chan(int key, int ch) => ch == 0 ? (key >> 16 & 0xff) : ch == 1 ? (key >> 8 & 0xff) : (key & 0xff);
    private static int WidestChannel(List<int> box)
    {
        int[] lo = { 255, 255, 255 }, hi = { 0, 0, 0 };
        foreach (int k in box) for (int c = 0; c < 3; c++) { int v = Chan(k, c); if (v < lo[c]) lo[c] = v; if (v > hi[c]) hi[c] = v; }
        int rr = hi[0] - lo[0], gr = hi[1] - lo[1], br = hi[2] - lo[2];
        return rr >= gr && rr >= br ? 0 : gr >= br ? 1 : 2;
    }
}
