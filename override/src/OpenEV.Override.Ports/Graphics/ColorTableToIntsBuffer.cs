using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10071764 (EV Override-11.c 46614-46646) — snapshot a CTabHandle's RGB entries
// into a fixed-point buffer (three Fixed components per colour = channel << 16). The
// CTabHandle is a ColorTable-registry key (not an EvoMemory address); returns null for
// a 0 handle (callers' Mac `!= 0` guard becomes `!= null`).
public static class ColorTableToIntsBuffer
{
    public static int[] Run(int colorTableHandle)
    {
        if (colorTableHandle == 0)
            return null;

        int savedState = MacToolbox.HGetState(colorTableHandle);
        MacToolbox.HNoPurge(colorTableHandle);
        MacToolbox.HLock(colorTableHandle);
        int[] outBuffer = new int[768];   // 256 colours x 3 Fixed components
        int count = MacToolbox.ColorTableEntryCount(colorTableHandle);
        for (short entryIndex = 0; entryIndex <= count; entryIndex++)
        {
            MacToolbox.GetColorTableRGB(colorTableHandle, entryIndex, out short r, out short g, out short b);
            int dst = entryIndex * 3;
            outBuffer[dst] = (ushort)r << 16;
            outBuffer[dst + 1] = (ushort)g << 16;
            outBuffer[dst + 2] = (ushort)b << 16;
        }
        MacToolbox.HSetState(colorTableHandle, (byte)savedState);
        return outBuffer;
    }
}
