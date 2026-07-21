using OpenEV.Override.Ports.Core.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007b1a8 — EV Override-11.c lines 52730-52766.
//
// Runs the GWorld op at `pixMapContainer` for the bounds Rect {boundsTopLeft, boundsBotRight},
// then disposes the pixmap's base-pointer, unwinding the depth-dependent alignment padding the
// allocator added (-4 at 1-bit, -8 at 8-bit).
//
// `pixMapContainer` is an int* (*param_1) whose first word is the pixmap pointer; its base field
// is at *pixMapContainer + 2. (param_2/param_3 spill to an adjacent stack pair the op reads by
// address — they are NOT a lockFlag/unused scalar.)
public static class DisposePixMapBaseWithAlignmentUnwind
{
    // Managed overload: operate on the GWorld sub-record {port, gdevice, rowTable} held
    // in GlobalState fields (by ref) through the GWorld create/decode op (CallGWorldOpOrFatal
    // -> DecodePictResource), then unwind + free the new port's pixel buffer.
    public static void Run(ref int port, ref int gdevice, ref int rowTable, int boundsTopLeft, int boundsBotRight)
    {
        short[] boundsRect = { (short)(boundsTopLeft >> 16), (short)boundsTopLeft,
                               (short)(boundsBotRight >> 16), (short)boundsBotRight };
        // This is a throwaway probe GWorld (created then immediately freed below), so the
        // created portRect (the stage-Rect record fields) is discarded.
        CallGWorldOpOrFatal.Run(ref port, ref gdevice, ref rowTable, boundsRect, out _, out _);

        int cPort = port;   // *param_1 — the new port
        if (GlobalState.ColorQuickDrawFlag == 0)
        {
            // DEAD: both call sites live in InitRenderWindow AFTER its
            // unconditional ColorQuickDrawFlag = 1, and nothing clears the flag.
            // The Mac B&W path: read the old-style port's baseAddr field (+2),
            // unwind the depth-1 alignment nudge (base -= 4 when set), DisposePtr
            // it, zero the field. If a B&W mode ever returns, ZeroPixMapBaseAndDispose.cs
            // has the closest analogous dead-B&W-branch pattern to re-derive from.
            throw new System.NotSupportedException(
                "DisposePixMapBase: B&W path ran (ColorQuickDrawFlag pinning changed?) — re-derive the baseAddr unwind.");
        }
        else
        {
            // MANAGED PIXELS: the alignment-unwind + DisposePtr + zero-baseAddr
            // sequence collapses to dropping the buffer.
            MacPixMaps.At(MacToolbox.GetPortPixMap(cPort)).Pixels = null;
        }
    }
}
