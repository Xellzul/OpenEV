using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_100780e4 — EV Override-11.c lines 50937-51029. Build the PixMap for one
// offscreen GWorld: allocate (or clone) its colour-table handle and a depth-sized
// pixel buffer, then fill the PixMap. Returns a Mac OSErr (0 = success); on any
// failure after the colour handle is acquired, that handle is freed.
//
// MANAGED: the PixMap is a MacPixMap object now (byte[] pixels + typed fields)
// reached through the registry handle the caller passes (GetPortPixMap). The
// colour table stays a raw Mac CTabHandle (later slice).
//
// `boundsTopLeft`/`boundsBotRight` are the packed bounds-Rect halves; top/bottom
// are their UPPER 16 bits (>> 16), not a separate left/right word — do not
// simplify that shift away.
public static class NewGWorld
{
    public static int Run(short depth, int boundsTopLeft, int boundsBotRight, int colorTable, ushort rowBytes, int pixMapHandle)
    {
        short top = (short)(boundsTopLeft >> 16);
        short bottom = (short)(boundsBotRight >> 16);

        // Colour table: indexed depths (<9) get a private CLONE of the caller's
        // colour table via the registry (CloneColorTable is the managed equivalent
        // of the original's HandToHand duplicate-the-handle idiom — HandToHand
        // itself is a no-op stub in this port). RGBDirect depths (>=9) build a
        // fresh stub handle in the fill section below.
        int cTableHandle;
        short err;
        if (depth < 9)
        {
            cTableHandle = MacToolbox.CloneColorTable(colorTable);
            // DEVIATION (faithful): MacToolbox.MemError() is a 0-stub and can't reflect a real
            // clone failure, so memFullErr is synthesized here to surface the same OSErr a failed
            // HandToHand would (mirrors the NewPtr fallback below).
            err = cTableHandle != 0 ? (short)0 : (short)-108;   // memFullErr
        }
        else
        {
            cTableHandle = 0;
            err = MacToolbox.MemError();
        }
        if (err != 0)
        {
            return err;   // nothing to free: the clone/alloc failed
        }

        // Pixel buffer: depth 1 and 8 over-allocate and nudge the origin for alignment.
        int rowSpan = (int)(short)(bottom - top) * (int)(short)rowBytes;
        byte[]? pixels;
        int pixelOrigin = 0;
        if (depth == 1)
        {
            pixels = rowSpan + 4 >= 0 ? new byte[rowSpan + 4] : null;
            pixelOrigin = 4;
        }
        else if (depth == 8)
        {
            pixels = rowSpan + 16 >= 0 ? new byte[rowSpan + 16] : null;
            pixelOrigin = 8;
        }
        else
        {
            pixels = rowSpan >= 0 ? new byte[rowSpan] : null;
        }
        if (pixels == null)
        {
            err = MacToolbox.MemError();
            // DEVIATION (faithful): MemError() is a 0-stub, so memFullErr is synthesized here to
            // surface NewPtr's failure the way the real trap would.
            if (err == 0) err = -108;   // memFullErr
            MacToolbox.DisposeCTable(cTableHandle);
            return err;
        }

        var pixMap = MacPixMaps.At(pixMapHandle);
        pixMap.Pixels = pixels;
        pixMap.PixelOrigin = pixelOrigin;
        pixMap.RowBytes = rowBytes;
        pixMap.SetBounds(boundsTopLeft, boundsBotRight);
        pixMap.PmVersion = 0;
        pixMap.PackType = 0;
        pixMap.PackSize = 0;
        pixMap.HRes = 0x480000;   // Fixed 72.0 dpi
        pixMap.VRes = 0x480000;
        pixMap.PixelSize = depth;
        pixMap.PlaneBytes = 0;
        pixMap.PmReserved = 0;
        if (depth < 9)
        {
            pixMap.PixelType = 0;    // indexed
            pixMap.CmpCount = 1;
            pixMap.CmpSize = depth;
            pixMap.ColorTableHandle = cTableHandle;
        }
        else
        {
            pixMap.PixelType = 0x10;   // RGBDirect
            pixMap.CmpCount = 3;
            pixMap.CmpSize = depth == 16 ? (short)5 : (short)8;
            // DEVIATION (faithful, catalogued, dead in practice): the decompile allocates
            // this stub CTab BEFORE the pixel buffer, so a pixel-alloc failure disposes it;
            // the port allocates it here instead (after success), so that dispose never
            // fires. Unreachable today (a negative rowSpan is the only failure mode above,
            // and this branch runs only once that's already ruled out) — see
            // tools/final_audit/BUGS.md FUN_100780e4 dec:51022.
            cTableHandle = MacToolbox.NewColorTable(pixMap.CmpSize * 3, count: 1);   // one entry, per decompile
            pixMap.ColorTableHandle = cTableHandle;
        }
        return 0;
    }
}
