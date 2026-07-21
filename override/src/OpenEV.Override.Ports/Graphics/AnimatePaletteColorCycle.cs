using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10070244 (EV Override-11.c 45870-45937) — colour-cycle the active GDevice colour
// table toward the RGBColor at `targetRgbPtr` over `stepCount` steps via a fixed-point
// per-step delta, then re-seed + snapshot. Inert in the port (gated on
// ColorQuickDrawAvailable, never set). `targetRgbPtr` is a raw RGBColor address (the
// screen-fade colour record behind Palette.ScreenFadeCTab), read once via ReadRGBColor.
public static class AnimatePaletteColorCycle
{
    public static void Run(short stepCount, int targetRgbPtr)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int gDevice = PaletteState.GDevice;
        int clutHandle = MacToolbox.DeviceColorTable(gDevice);
        short colorCount = (short)MacToolbox.ColorTableEntryCount(clutHandle);

        int[] srcColorTable = ColorTableToIntsBuffer.Run(clutHandle);
        if (srcColorTable == null)
            return;
        int[] deltaTable = new int[768];   // 256 colours x 3 Fixed components

        MacToolbox.ReadRGBColor(targetRgbPtr, out short tr, out short tg, out short tb);
        ushort targetRed = (ushort)tr;
        ushort targetGreen = (ushort)tg;
        ushort targetBlue = (ushort)tb;
        int stepCountInt = stepCount;
        for (short step = 0; step <= colorCount; step++)
        {
            int colorOffset = step * 3;
            // Logical shift: the decompile does *(uint*) >> 2; an arithmetic >> 2 would
            // corrupt channels >= 0x8000.
            deltaTable[colorOffset] =
                 (int)(((uint)srcColorTable[colorOffset] >> 2) + (uint)(targetRed * -0x4000)) / stepCountInt << 2;
            deltaTable[colorOffset + 1] =
                 (int)(((uint)srcColorTable[colorOffset + 1] >> 2) + (uint)(targetGreen * -0x4000)) / stepCountInt << 2;
            deltaTable[colorOffset + 2] =
                 (int)(((uint)srcColorTable[colorOffset + 2] >> 2) + (uint)(targetBlue * -0x4000)) / stepCountInt << 2;
        }
        int savedGDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gDevice);
        int handleState = MacToolbox.HGetState(clutHandle);
        MacToolbox.HNoPurge(clutHandle);
        MacToolbox.HLock(clutHandle);
        for (short step = 0; step < stepCount; step++)
        {
            Palette.Subtract(srcColorTable, deltaTable, colorCount);
            Palette.IntsBufferToColorTable(srcColorTable, clutHandle);
            MacToolbox.SetColorTableEntries(clutHandle, 0, colorCount);
        }
        MacToolbox.HSetState(clutHandle, (byte)handleState);
        MacToolbox.MakeITable(0, 0, 0);
        MacToolbox.SetColorTableSeed(clutHandle, MacToolbox.GetCTSeed());
        Palette.SnapshotCTable(PaletteState.SavedCTab, out PaletteState.SavedSeed);
        MacToolbox.SetGDevice(savedGDevice);
    }
}
