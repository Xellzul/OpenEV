using System;
using System.Collections.Generic;
using System.Text;
using static FreeTypeSharp.FT;
using FreeTypeSharp;

namespace OpenEV.Platform.Imaging;

// Rasterize a 1-bit Mac font strike at runtime by running the sfnt's own TrueType hinting
// bytecode through FreeType's interpreter — the thing stb lacks. Produces a MacBitmapFont
// from the user's own extracted sfnt, so the game ships no font-derived bitmap (this replaced
// the offline-baked Sillycon-14.bdf, approved 2026-07-19; see SillyconFont for fidelity notes).
// Returns null on any native/init failure (missing runtime, unmapped face) so callers fall
// back to the stb Monochrome outline.
public static unsafe class FreeTypeStrikeRasterizer
{
    // FT_LOAD_RENDER (0x4) | FT_LOAD_TARGET_MONO ((FT_RENDER_MODE_MONO=2) << 16 = 0x20000).
    // TARGET_MONO makes the hinter grid-fit for a 1-bit target and RENDER rasterizes to a mono
    // bitmap in one call — the classic B/W path, matching the Mac's 1-bit HUD text.
    private const FT_LOAD LoadMonoRender = (FT_LOAD)0x20004;

    private static readonly Encoding? MacRoman = TryGetMacRoman();
    private static Encoding? TryGetMacRoman()
    {
        try { return Encoding.GetEncoding(10000); } catch { return null; }
    }

    public static MacBitmapFont? TryRasterize(byte[] sfnt, int pixelSize, int ascent, int descent)
    {
        if (sfnt is null || sfnt.Length == 0) return null;
        FT_LibraryRec_* lib = null;
        FT_FaceRec_* face = null;
        try
        {
            if (FT_Init_FreeType(&lib) != FT_Error.FT_Err_Ok || lib == null) return null;

            // TT_INTERPRETER_VERSION_35 = the original bytecode interpreter (classic B/W grid-fit),
            // set BEFORE the face loads. Closest of FreeType's modes to Apple's scaler.
            uint v35 = 35;
            SetTrueTypeProperty(lib, "interpreter-version", &v35);

            fixed (byte* basePtr = sfnt)
            {
                if (FT_New_Memory_Face(lib, basePtr, (IntPtr)sfnt.Length, IntPtr.Zero, &face) != FT_Error.FT_Err_Ok || face == null)
                    return null;

                if (FT_Select_Charmap(face, FT_Encoding_.FT_ENCODING_UNICODE) != FT_Error.FT_Err_Ok)
                    return null;
                if (FT_Set_Pixel_Sizes(face, 0, (uint)pixelSize) != FT_Error.FT_Err_Ok)
                    return null;

                var glyphs = new List<MacBitmapFont.GlyphSpec>(96);
                for (int b = 0x20; b <= 0xFF; b++)
                {
                    char ch = MacRoman is not null ? MacRoman.GetString(new[] { (byte)b })[0] : (char)b;
                    uint gid = (uint)FT_Get_Char_Index(face, (UIntPtr)ch);
                    if (gid == 0) continue;
                    if (FT_Load_Glyph(face, gid, LoadMonoRender) != FT_Error.FT_Err_Ok) continue;

                    FT_GlyphSlotRec_* slot = face->glyph;
                    int advance = (int)((long)slot->advance.x >> 6);
                    var spec = DecodeGlyph(ch, advance, slot);
                    glyphs.Add(spec);
                }
                if (glyphs.Count == 0) return null;
                return MacBitmapFont.FromGlyphs(ascent, descent, pixelSize, glyphs);
            }
        }
        catch
        {
            return null;   // any native marshalling failure → let the caller fall back
        }
        finally
        {
            if (face != null) FT_Done_Face(face);
            if (lib != null) FT_Done_FreeType(lib);
        }
    }

    private static MacBitmapFont.GlyphSpec DecodeGlyph(char ch, int advance, FT_GlyphSlotRec_* slot)
    {
        ref FT_Bitmap_ bmp = ref slot->bitmap;
        int w = (int)bmp.width, h = (int)bmp.rows;
        int offX = slot->bitmap_left;
        int offY = -slot->bitmap_top;   // Glyph convention: top relative to baseline, negative above

        if (w <= 0 || h <= 0 || bmp.buffer == null || bmp.pixel_mode != FT_Pixel_Mode_.FT_PIXEL_MODE_MONO)
            return new MacBitmapFont.GlyphSpec(ch, advance, offX, offY, 0, 0, null);

        int pitch = bmp.pitch;   // bytes per row, MSB-first packed 1-bit
        var img = new Rgba8Image(w, h);
        byte[] px = img.Pixels;
        for (int r = 0; r < h; r++)
        {
            byte* row = bmp.buffer + r * pitch;
            for (int c = 0; c < w; c++)
            {
                if (((row[c >> 3] >> (7 - (c & 7))) & 1) == 0) continue;
                int o = (r * w + c) * 4;
                px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 255;   // premultiplied white
            }
        }
        return new MacBitmapFont.GlyphSpec(ch, advance, offX, offY, w, h, img);
    }

    private static void SetTrueTypeProperty(FT_LibraryRec_* lib, string property, void* value)
    {
        byte[] mod = Encoding.ASCII.GetBytes("truetype\0");
        byte[] prop = Encoding.ASCII.GetBytes(property + "\0");
        fixed (byte* mp = mod)
        fixed (byte* pp = prop)
            FT_Property_Set(lib, mp, pp, value);
    }
}
