using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10078dec from EV Override-11.c lines 51412-51560.
//
// Generic PICT drawer: load PICT `pictId` and draw it per `drawMode`
//   1 — into the caller's dest rect,
//   0 — at the PICT's own frame (picFrame @ resource data +2),
//   2 — centred inside the caller's dest rect.
// The dest rect arrives as two packed {top,left}/{bottom,right} ints (the Mac
// callers LoadPictBlit_Mode1/2 pass &stack rect halves) → a local short[4].
//
// Three branches: handle already loaded in memory; load-now (plenty of free
// memory); and the LOW-MEMORY progressive path that swaps a custom QD getPic
// bottleneck into the port and streams the resource from disk through
// ReadPartialResourceAdvance. The progressive path is DEAD in the port (the
// FreeMem() stub reports 256MB, so `resSizeOnDisk + 20000 < freeMem` always
// holds) — kept faithful.
//
// Remaining raw reads are the PICT resource-handle contents (sanctioned
// boundary) and the saved port's version/grafProcs fields via the
// dual-dispatching MacToolbox accessors.
public static class DrawPictResource
{
    public static void Run(short pictId, int destTopLeft, int destBotRight, byte drawMode)
    {
        short[] destRect =
        {
            (short)(destTopLeft  >> 16), (short)destTopLeft,    // top, left  (sStack1c/1e)
            (short)(destBotRight >> 16), (short)destBotRight,   // bottom, right (sStack20/22)
        };

        // *piVar1 = GetResource('PICT', pictId) with SetResLoad(0) — park the
        // handle in the shared streaming field (the decompile's *_DAT_10081aa8;
        // ReadPartialResourceAdvance reads the same park).
        MacToolbox.SetResLoad(false);
        ResourceGlobals.PartialResStreamHandle = MacToolbox.GetResource(MacResType.Pict, (int)pictId);
        MacToolbox.SetResLoad(true);
        int pictHandle = ResourceGlobals.PartialResStreamHandle;
        if (pictHandle == 0)
            return;

        // Master pointer probe: 0 = not in memory yet (the managed registry
        // always has the bytes, so this matches the old ReadInt(handle) test).
        if (MacToolbox.ResourceBytes(pictHandle) is null)
        {
            int freeMem = MacToolbox.FreeMem();
            int resSizeOnDisk = MacToolbox.GetResourceSizeOnDisk(pictHandle);
            bool partialTrapAvailable = IsMacTrapAvailable2.Run(0xffffa822);
            if (!partialTrapAvailable || resSizeOnDisk + 20000 < freeMem)
            {
                // Plenty of memory: load and draw whole.
                MacToolbox.LoadResource(pictHandle);
                if ((short)MacToolbox.ResError() == 0)
                    DrawPictByMode(pictHandle, drawMode, destRect);
                MacToolbox.ReleaseResource(pictHandle);
            }
            else
            {
                // Low-memory progressive path (dead in the port — see header): install the
                // custom getPic bottleneck on the current port and stream the PICT.
                int[] portOut = new int[1];
                MacToolbox.GetPort(portOut);
                int port = portOut[0];

                // Two 44-byte QD-procs records (decompile stack buffers auStack_b8/
                // auStack_84): their ADDRESS lands in the port record's grafProcs —
                // the port/window record is the deferred-campaign raw boundary, so
                // they stay scratch-arena blocks. SetStd[C]Procs are no-op stubs and
                // the bytes are never read back through these blocks.
                int routineDescStd = 0;
                int routineDescColor = 0;
                if (MacToolbox.BitAnd(MacToolbox.GetPortVersion(port), -0x8000) == 0)
                {
                    // SetStdProcs/SetPortGrafProcs are no-op stubs — the QDProcs
                    // record bytes are never read back (was a scratch block).
                    MacToolbox.SetStdProcs(0);
                    MacToolbox.SetPortGrafProcs(port, 0);
                    routineDescStd = MacToolbox.NewRoutineDescriptor(ResourceGlobals.StreamGetPicProc, 0x2c0, 1);
                }
                else
                {
                    MacToolbox.SetStdCProcs(0);   // no-op stub — record never read back
                    MacToolbox.SetPortGrafProcs(port, 0);
                    routineDescColor = MacToolbox.NewRoutineDescriptor(ResourceGlobals.StreamGetPicProc, 0x2c0, 1);
                }

                // 10-byte header handle: picSize + picFrame, pre-read from disk.
                int streamHandle = MacToolbox.NewHandle(10);
                // The decompile derefs *streamHandle (the master pointer) for
                // ReadPartialResource's buffer arg; the trap is a stub on this
                // dead progressive path, so the handle token stands in instead.
                MacToolbox.ReadPartialResource(pictHandle, 0, streamHandle, 10);
                ResourceGlobals.PartialResStreamCursor = 10;   // seed the stream cursor past the 10-byte header

                DrawPictByMode(streamHandle, drawMode, destRect);

                if (MacToolbox.BitAnd(MacToolbox.GetPortVersion(port), -0x8000) == 0)
                    MacToolbox.DisposeRoutineDescriptor(routineDescStd);
                else
                    MacToolbox.DisposeRoutineDescriptor(routineDescColor);
                MacToolbox.SetPortGrafProcs(port, 0);
                MacToolbox.DisposeHandle(streamHandle);
                MacToolbox.ReleaseResource(pictHandle);
            }
        }
        else
        {
            // Already in memory: draw straight from the parked handle (no Release —
            // ORIGINAL behaviour, the resource stays cached).
            DrawPictByMode(pictHandle, drawMode, destRect);
        }
    }

    // Shared tail of the three branches: draw `pict` per drawMode. `pict` is a
    // PICT Handle (the parked resource or the 10-byte streaming header handle —
    // both carry picSize @+0 and picFrame {top,left,bottom,right} @+2..+9).
    private static void DrawPictByMode(int pict, byte drawMode, short[] destRect)
    {
        if (drawMode == 1)
        {
            MacToolbox.DrawPicture(pict, destRect);
        }
        else if (drawMode == 0)
        {
            // Draw at the PICT's own frame (the picFrame shorts at resource
            // bytes 2..8 — was the master-ptr+2 rect address).
            short[] ownFrame = { MacToolbox.ReadResourceShort(pict, 2), MacToolbox.ReadResourceShort(pict, 4),
                                 MacToolbox.ReadResourceShort(pict, 6), MacToolbox.ReadResourceShort(pict, 8) };
            MacToolbox.DrawPicture(pict, ownFrame);
        }
        else if (drawMode < 3)
        {
            // Centre the picFrame (resource bytes 2..8, same layout as above) inside destRect.
            short frameTop = MacToolbox.ReadResourceShort(pict, 2);
            short frameLeft = MacToolbox.ReadResourceShort(pict, 4);
            short frameBottom = MacToolbox.ReadResourceShort(pict, 6);
            short frameRight = MacToolbox.ReadResourceShort(pict, 8);
            short[] picRect = { frameTop, frameLeft, frameBottom, frameRight };
            // dh/dv = dest extent minus (frame far edge + frame near edge); the
            // decompile's `>>1 + negative-odd fixup` is signed round-toward-zero
            // division — C# int `/ 2` exactly.
            int dh = (short)((destRect[3] - destRect[1]) - frameRight - frameLeft);
            int dv = (short)((destRect[2] - destRect[0]) - frameBottom - frameTop);
            MacToolbox.OffsetRect(picRect, (short)(dh / 2), (short)(dv / 2));
            MacToolbox.DrawPicture(pict, picRect);
        }
    }
}
