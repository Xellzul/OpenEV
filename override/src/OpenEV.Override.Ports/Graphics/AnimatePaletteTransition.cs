using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10070478 (EV Override-11.c 45938-46009) — animate the active GDevice colour table
// toward the `targetClutHandle` palette over `stepCount` steps via a fixed-point per-step
// delta, then re-seed + snapshot. Inert in the port (gated on ColorQuickDrawAvailable,
// never set). Both colour tables are ColorTable-registry handles; the working buffers are
// managed int[] Fixed triples.
public static class AnimatePaletteTransition
{
    public static void Run(short stepCount, int targetClutHandle)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int gDevice = PaletteState.GDevice;
        int clutHandle = MacToolbox.DeviceColorTable(gDevice);
        short colorCount = (short)MacToolbox.ColorTableEntryCount(clutHandle);

        int[] srcColorTable = ColorTableToIntsBuffer.Run(targetClutHandle);
        if (srcColorTable == null)
            return;
        int[] destColorTable = ColorTableToIntsBuffer.Run(clutHandle);
        if (destColorTable == null)
            return;
        int[] deltaTable = new int[768];   // 256 colours x 3 Fixed components

        int stepCountInt = stepCount;
        for (short step = 0; step <= colorCount; step++)
        {
            int colorOffset = step * 3;
            // Logical shift: the decompile does *(uint*) >> 2; an arithmetic >> 2 would
            // corrupt channels >= 0x8000.
            deltaTable[colorOffset] =
                 (int)(((uint)destColorTable[colorOffset] >> 2) - ((uint)srcColorTable[colorOffset] >> 2)) / stepCountInt << 2;
            deltaTable[colorOffset + 1] =
                 (int)(((uint)destColorTable[colorOffset + 1] >> 2) - ((uint)srcColorTable[colorOffset + 1] >> 2)) / stepCountInt << 2;
            deltaTable[colorOffset + 2] =
                 (int)(((uint)destColorTable[colorOffset + 2] >> 2) - ((uint)srcColorTable[colorOffset + 2] >> 2)) / stepCountInt << 2;
        }
        int savedGDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gDevice);
        int handleState = MacToolbox.HGetState(clutHandle);
        MacToolbox.HNoPurge(clutHandle);
        MacToolbox.HLock(clutHandle);
        for (short step = 0; step < stepCount; step++)
        {
            Palette.Subtract(destColorTable, deltaTable, colorCount);
            Palette.IntsBufferToColorTable(destColorTable, clutHandle);
            MacToolbox.SetColorTableEntries(clutHandle, 0, colorCount);
        }
        MacToolbox.HSetState(clutHandle, (byte)handleState);
        MacToolbox.MakeITable(0, 0, 0);
        MacToolbox.SetColorTableSeed(clutHandle, MacToolbox.GetCTSeed());
        Palette.SnapshotCTable(PaletteState.SavedCTab, out PaletteState.SavedSeed);
        MacToolbox.SetGDevice(savedGDevice);
    }
}
