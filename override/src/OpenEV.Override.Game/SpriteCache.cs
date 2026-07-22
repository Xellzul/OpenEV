using System.Collections.Generic;
using OpenEV.Platform.EvoData;
using OpenEV.Platform.Imaging;

namespace OpenEV.Override.Game;

// Decodes EVO sprites for the in-game renderer. In this data set the sprite
// sheets are PICTs (not RleD): a spďn record gives SpritesId (sheet PICT),
// MasksId (mask PICT), XSize×YSize per frame, and an XTiles×YTiles grid of
// rotation frames. We decode both PICTs via PictDecoder, slice the sheet into
// frames, and fold the mask into each frame's alpha. Ship id N → spin N;
// spob → 300+type; missile/projectile → 200+graphic.
//
// Frames are kept STRAIGHT (non-premultiplied) RGBA — exactly what the old
// Texture2D uploads held — so Canvas.Blit's premultiplied-over composite
// reproduces the previous GPU output bit-for-bit (incl. EVO's black-backed
// sheets where transparent texels carry rgb=0).
internal sealed class SpriteCache
{
    private readonly OverrideGameData _data;
    private readonly Dictionary<int, Rgba8Image[]?> _cache = new();
    private readonly object _lock = new();

    public SpriteCache(OverrideGameData data) { _data = data; }

    public Rgba8Image[]? GetSpinFrames(int spinId)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(spinId, out var cached)) return cached;
            Rgba8Image[]? result = TryDecode(spinId);
            _cache[spinId] = result;
            return result;
        }
    }

    private Rgba8Image[]? TryDecode(int spinId)
    {
        if (!_data.Spins.TryGetValue(spinId, out var spin)) return null;
        int xt = spin.XTiles > 0 ? spin.XTiles : 1;
        int yt = spin.YTiles > 0 ? spin.YTiles : 1;
        int fw = spin.XSize > 0 ? spin.XSize : 1;
        int fh = spin.YSize > 0 ? spin.YSize : 1;

        var sheet = DecodePict(spin.SpritesId);
        if (sheet is null) return null;
        var mask = DecodePict(spin.MasksId);   // may be null → opaque

        // The original draws the sheet/mask PICTs via DrawPicture into a GWorld sized
        // XSize*XTiles × YSize*YTiles (LoadIconPairForSlot, FUN_1001e4fc) — QuickDraw
        // SCALES a picture whose frame differs, it never crops. Plug-in art relies on
        // this (E3's planet sheets are 80×80 PICTs in 80×75 spins). Same floor mapping
        // as Canvas.Blit's nearest-neighbor.
        int sheetW = fw * xt, sheetH = fh * yt;

        int count = xt * yt;
        var frames = new Rgba8Image[count];
        for (int f = 0; f < count; f++)
        {
            int col = f % xt, row = f / xt;
            int ox = col * fw, oy = row * fh;
            var bmp = new Rgba8Image(fw, fh);
            for (int y = 0; y < fh; y++)
            {
                for (int x = 0; x < fw; x++)
                {
                    int sx = (int)((long)(ox + x) * sheet.Width / sheetW);
                    int sy = (int)((long)(oy + y) * sheet.Height / sheetH);
                    byte r = 0, g = 0, b = 0, a = 255;
                    if (sx < sheet.Width && sy < sheet.Height)
                    {
                        int si = (sy * sheet.Width + sx) * 4;
                        r = sheet.Pixels[si]; g = sheet.Pixels[si + 1]; b = sheet.Pixels[si + 2];
                    }
                    if (mask is not null)
                    {
                        int mx = (int)((long)(ox + x) * mask.Width / sheetW);
                        int my = (int)((long)(oy + y) * mask.Height / sheetH);
                        if (mx < mask.Width && my < mask.Height)
                        {
                            // EVO mask: white = opaque, black = transparent.
                            int mi = (my * mask.Width + mx) * 4;
                            a = mask.Pixels[mi]; // grayscale → red channel
                        }
                    }
                    bmp.SetPixel(x, y, r, g, b, a);
                }
            }
            frames[f] = bmp;
        }
        return frames;
    }

    private Rgba8Image? DecodePict(int id)
    {
        if (id == 0 || !_data.Picts.TryGetValue(id, out var bytes)) return null;
        try { return PictDecoder.Decode(bytes, $"PICT {id}"); }
        catch (System.Exception ex) { System.Console.WriteLine($"[SpriteCache] PICT {id} decode failed: {ex.Message}"); return null; }
    }
}
