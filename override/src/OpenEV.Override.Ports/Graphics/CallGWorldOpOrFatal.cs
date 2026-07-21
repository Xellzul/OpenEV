using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1007962c (decompile 51688-51707) — forward to DecodePictResource (the offscreen
// GWorld create/decode op) on the GWorld sub-record; a non-zero status means out of memory
// and fatally exits with the "Out of memory!..." alert.
public static class CallGWorldOpOrFatal
{
    // The GWorld sub-record {port, gdevice, rowTable} is threaded by ref; the created GWorld's
    // portRect is threaded back out so the caller can seed its record's stage Rect (see
    // DecodePictResource for why dropping it broke the landed-screen black-out).
    public static void Run(ref int port, ref int gdevice, ref int rowTable, short[] boundsRect,
                           out int portRectTopLeft, out int portRectBotRight)
    {
        short status = (short)DecodePictResource.Run(ref port, ref gdevice, ref rowTable,
                                                     boundsRect, out portRectTopLeft, out portRectBotRight);
        if (status != 0)
            // Message from data-seg cell 0x1008594c (StaticData.UiErrorStrings[OutOfMemoryMessageIndex]).
            FatalOutOfMemoryExit.Run(StaticData.UiErrorStrings[StaticData.OutOfMemoryMessageIndex]);
    }
}
