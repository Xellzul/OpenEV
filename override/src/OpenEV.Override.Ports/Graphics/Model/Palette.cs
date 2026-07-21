using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics.Model;

// Managed home for the colour-table / palette subsystem (the InitColorTables /
// InstallColorEntries / InstallScreenPalette / preset / snapshot / fade / HUD-colour ports
// plus the FUN_10070f74 CloneActiveColorTable).
//
// The CLUT machinery here is INERT in the port: the install/preset/snapshot/fade table work is
// gated on RenderGlobals.ColorQuickDrawAvailable (0x100823f4), which is read in ~18 places but
// written nowhere -> always 0, so every install helper early-returns after its host-bridge
// line. Kept faithful to the decompile for parity. The VISIBLE screen-palette effects come
// from the host-substrate bridges, mirroring the screen-fade pattern: FadeIn/FadeOut pair
// with MacToolbox.ScreenFadeToColor/ToImage, and the cloak preset installs pair with
// MacToolbox.ScreenPaletteRemap (InstallScreenPalette itself clears the remap on any applied
// install). The palette CTabHandle cells were raw EvoMemory-addressed BSS cells on the
// original Mac binary; they are plain managed fields now (below) — PaletteTableArraySlot's
// slot const is kept only to document that it also aliased the cloak preset-index table on
// the Mac, and the HUD/dialog/galaxy colour cells are managed UiColors fields too.
public static class Palette
{
    // The original Mac CTabHandle-pointer cells, retained only to document the original
    // addresses; storage is the managed fields below. PaletteTableArraySlot was also the
    // cloak preset-index table (that aliasing reader now reads stale memory).
    public const int ActivePaletteHandleSlot = 0x1008120c;
    public const int PaletteTableArraySlot = 0x10081208;
    public const int DefaultPaletteHandleSlot = 0x10080f68;

    // The active / 6 per-preset screen-palette CTabHandles (from GetCTable). The DEFAULT
    // screen-palette handle for the same decompile cell 0x10080f68 is ScreenPaletteCTab
    // below — see its split-field note.
    public static int ActiveCTable;
    public static readonly int[] PresetCTables = new int[7];

    // The HUD / dialog / galaxy colour globals are managed packed-0xRRGGBB fields on UiColors
    // now (raw cells retired). SetHudColors*/InitHudColors set those directly. The decompile's
    // two overlap writes (a chatter buffer + a screen-fade CTabHandle, neither a colour) are
    // dropped — in the managed model there is no overlap.

    // FUN_1005d1a8 — allocate the default + active + 6 per-preset palettes and install them.
    // (The CLUT installs stay inert — gated on ColorQuickDrawAvailable — and the per-preset
    // clones come back 0 for the same reason; only the default handle survives as a real
    // registry table.)
    public static void Init()
    {
        ScreenPaletteCTab = MacToolbox.GetCTable(8);
        ActiveCTable = MacToolbox.GetCTable(8);

        // Faithful: the decompile assigns the active handle from GetCTable twice here (the
        // first result is overwritten unused).
        ActiveCTable = MacToolbox.GetCTable(8);
        // The decompile installs an all-white RGBColor into every entry (full intensity).
        InstallColorEntries(-1, -1, -1, 0);
        ActiveCTable = CloneActiveColorTable();
        InstallScreenPalette(ScreenPaletteCTab, 1);

        for (int preset = 1; preset < PresetCTables.Length; preset++)
        {
            PresetCTables[preset] = MacToolbox.GetCTable(8);
            PresetPaletteColor((short)preset);
            PresetCTables[preset] = CloneActiveColorTable();
            InstallScreenPalette(ScreenPaletteCTab, 1);
        }
    }

    // FUN_10070f74 — clone the active GDevice colour table into a fresh Handle. Returns 0 when
    // colour QD is unavailable. BlockMove is itself a no-op stub, so the byte copy does not
    // actually happen yet — kept structurally faithful, not faked.
    public static int CloneActiveColorTable()
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return 0;
        return MacToolbox.CloneColorTable(PaletteState.SavedCTab);
    }

    // FUN_100707e0 — push one RGB triple into every entry of the active GDevice colour table,
    // then re-seed/install it. `apply` != 0 -> SetEntries.
    public static void InstallColorEntries(short red, short green, short blue, byte apply)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int gdevice = PaletteState.GDevice;
        int ctHandle = MacToolbox.DeviceColorTable(gdevice);
        int entryCount = MacToolbox.ColorTableEntryCount(ctHandle);

        for (int i = 0; i <= entryCount; i++)
            MacToolbox.SetColorTableRGB(ctHandle, i, red, green, blue);

        int savedGDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gdevice);
        int handleState = MacToolbox.HGetState(ctHandle);
        MacToolbox.HNoPurge(ctHandle);
        MacToolbox.HLock(ctHandle);
        if (apply != 0)
            MacToolbox.SetColorTableEntries(ctHandle, 0, entryCount);
        MacToolbox.HSetState(ctHandle, (byte)handleState);
        MacToolbox.MakeITable(0, 0, 0);
        MacToolbox.SetColorTableSeed(ctHandle, MacToolbox.GetCTSeed());
        SnapshotActivePalette();
        MacToolbox.SetGDevice(savedGDevice);
    }

    // Pointer overload for external callers that hold a raw Mac RGBColor address.
    public static void InstallColorEntries(int rgbPtr, byte apply)
    {
        MacToolbox.ReadRGBColor(rgbPtr, out short r, out short g, out short b);
        InstallColorEntries(r, g, b, apply);
    }

    // The DEFAULT screen-palette CTabHandle (decompile **(GameToc-0x76f8), cell 0x10080f68) —
    // what FUN_1005d1a8 seeds with GetCTable(8) and every restore-path install passes.
    // FIXED split-field bug: this cell used to exist TWICE in the managed model (a
    // `DefaultCTable` field that Init wrote, and this one that every install-caller read) —
    // so all restores installed handle 0. One field now; Init seeds it.
    public static int ScreenPaletteCTab;

    // The screen-fade colour record pointer (decompile *(GameToc-0x7860), cell 0x10080e00).
    // The original never writes it -> reads 0, and the fade paths treat it as black.
    public static int ScreenFadeCTab;

    // The credits-screen palette CTabHandle (decompile **(GameToc-0x75b4), cell 0x100810ac).
    // ApplyCreditsScreenFade stores GetCTable(1000) into it (still a 0-returning stub today, so
    // installs no-op).
    public static int CreditsPaletteCTab;

    // The credits-fade "primed" byte (decompile *_DAT_100810b0). No ported writer yet -> stays 0
    // = ApplyCreditsScreenFade always primes the fade-in.
    public static byte CreditsFadePrimed;

    // FUN_100700d0 — copy a source colour table's RGB entries into the active GDevice colour
    // table, then re-seed/install. `srcCtHandle` is a CTabHandle. `apply` != 0 -> SetEntries.
    public static void InstallScreenPalette(int srcCtHandle, byte apply)
    {
        // Host bridge (before the colour-QD gate, like FadeIn's ScreenFadeToColor): an
        // APPLIED install (SetEntries) replaces the whole visible hardware CLUT, which drops
        // any cloak screen-palette remap (the disengage path installs the default table this
        // way); the cloak's own preset install immediately re-arms it via ScreenPaletteRemap.
        // apply==0 installs never touched the hardware palette, so the visible remap stays.
        if (apply != 0)
            MacToolbox.ScreenPaletteRestore();

        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int gdevice = PaletteState.GDevice;
        int ctHandle = MacToolbox.DeviceColorTable(gdevice);
        int savedGDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gdevice);
        int handleState = MacToolbox.HGetState(ctHandle);
        MacToolbox.HNoPurge(ctHandle);
        MacToolbox.HLock(ctHandle);
        MacToolbox.GetHandleSize(ctHandle);
        int entryCount = MacToolbox.ColorTableEntryCount(ctHandle);

        MacToolbox.CopyColorTableRGB(ctHandle, srcCtHandle, entryCount);

        if (apply != 0)
            MacToolbox.SetColorTableEntries(ctHandle, 0, entryCount);
        MacToolbox.MakeITable(0, 0, 0);
        MacToolbox.SetColorTableSeed(ctHandle, MacToolbox.GetCTSeed());
        SnapshotActivePalette();
        MacToolbox.HSetState(ctHandle, (byte)handleState);
        MacToolbox.SetGDevice(savedGDevice);
    }

    // Shared tail of the two installers — snapshot the just-installed palette.
    private static void SnapshotActivePalette()
        => SnapshotCTable(PaletteState.SavedCTab, out PaletteState.SavedSeed);

    // FUN_10070ff8 — snapshot the active GDevice colour table into the `destCtHandle`
    // CTabHandle (re-seeding each ColorSpec.value to its index) and return its first colour.
    public static void SnapshotCTable(int destCtHandle, out int firstColor)
    {
        firstColor = 0;
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int gdevice = PaletteState.GDevice;
        int ctHandle = MacToolbox.DeviceColorTable(gdevice);
        int savedDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gdevice);
        int savedHandleState = MacToolbox.HGetState(ctHandle);
        MacToolbox.HNoPurge(ctHandle);
        MacToolbox.HLock(ctHandle);
        int size = MacToolbox.GetHandleSize(ctHandle);
        MacToolbox.BlockMoveColorTableData(destCtHandle, ctHandle, size);

        int count = MacToolbox.ColorTableEntryCount(ctHandle);
        for (int i = 0; i <= count; i++)
            MacToolbox.SetColorTableEntryValue(destCtHandle, i, (short)i);

        firstColor = MacToolbox.ColorTableSeed(ctHandle);
        MacToolbox.HSetState(ctHandle, (byte)savedHandleState);
        MacToolbox.SetGDevice(savedDevice);
    }

    // FUN_1007093c — remap the active palette toward (targetR,targetG,targetB) by scaling each
    // entry's RGB by the ratio of the target colour's HSL value to the entry's HSL value,
    // through a fixed-point ints buffer, then install.
    public static void RemapToHSL(short targetR, short targetG, short targetB, byte applyEntries)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int targetDevice = PaletteState.GDevice;
        int colorSpec = MacToolbox.DeviceColorTable(targetDevice);
        int entryCount = MacToolbox.ColorTableEntryCount(colorSpec);

        int[] newColorBuf = new int[(entryCount + 1) * 3];   // 3 Fixed components per entry

        for (int i = 0; i <= entryCount; i++)
        {
            MacToolbox.GetColorTableRGB(colorSpec, i, out short er, out short eg, out short eb);
            long entryValue = MacToolbox.RGB2HSLValue(er, eg, eb);   // unsigned lightness 0..0xFFFF

            // entry -> hue × L(entry): the capture-derived composite of the original's
            // RGB2HSL + FixRatio(entryL, targetL) + FixMul(ratio, Long2Fix(hue)) OS-glue
            // chain — for the full-channel preset hues it is the IDENTITY on the entry's
            // unsigned lightness (see MacToolbox.RGB2HSLValue's ground-truth note).
            // Channel lands in each Fixed's high word (FUN_100716e0 extracts it).
            int e = i * 3;
            newColorBuf[e] = (int)(((entryValue * (ushort)targetR) >> 16) << 16);
            newColorBuf[e + 1] = (int)(((entryValue * (ushort)targetG) >> 16) << 16);
            newColorBuf[e + 2] = (int)(((entryValue * (ushort)targetB) >> 16) << 16);
        }

        int savedDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(targetDevice);
        int savedHandleState = MacToolbox.HGetState(colorSpec);
        MacToolbox.HNoPurge(colorSpec);
        MacToolbox.HLock(colorSpec);
        IntsBufferToColorTable(newColorBuf, colorSpec);
        if (applyEntries != 0)
            MacToolbox.SetColorTableEntries(colorSpec, 0, entryCount);
        MacToolbox.HSetState(colorSpec, (byte)savedHandleState);
        MacToolbox.MakeITable(0, 0, 0);
        MacToolbox.SetColorTableSeed(colorSpec, MacToolbox.GetCTSeed());
        SnapshotCTable(PaletteState.SavedCTab, out PaletteState.SavedSeed);
        MacToolbox.SetGDevice(savedDevice);
    }

    // FUN_100706b4 — select a preset hue (full RGB triple) and remap the palette toward it. The
    // second decompile arg is an indeterminate register; pass 0 (don't re-install entries).
    public static void PresetPaletteColor(short preset)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;
        if (PresetHue(preset, out short r, out short g, out short b))
            RemapToHSL(r, g, b, 0);
    }

    // FUN_100706b4's hue table, shared with the host cloak bridge (EngageCloaking /
    // ReapplyCloakPalette pass the same triple to MacToolbox.ScreenPaletteRemap that the
    // boot-time preset build fed to RemapToHSL). False for preset 0 / out-of-range (no remap).
    public static bool PresetHue(short preset, out short r, out short g, out short b)
    {
        const short Full = -1, Half = 32767;   // 16-bit channel intensity: 0xffff full, 0x7fff half
        switch (preset)
        {
            case 1: (r, g, b) = (Full, 0, 0); return true; // red
            case 2: (r, g, b) = (0, Full, 0); return true; // green
            case 3: (r, g, b) = (0, 0, Full); return true; // blue
            case 4: (r, g, b) = (0, Full, Full); return true; // cyan
            case 5: (r, g, b) = (Full, 0, Full); return true; // magenta
            case 6: (r, g, b) = (Full, Full, 0); return true; // yellow
            case 7: (r, g, b) = (Half, Half, Half); return true; // grey
            default: (r, g, b) = (0, 0, 0); return false; // case 0 and any other index: no remap
        }
    }

    // FUN_100716e0 — write a fixed-point ints buffer (three 32-bit components per entry) into a
    // Mac ColorTable's RGB shorts, taking each component's high word. Used by RemapToHSL and the
    // palette fades/animators.
    public static void IntsBufferToColorTable(int[] intsBuffer, int colorTableHandle)
    {
        if (colorTableHandle == 0 || intsBuffer == null)
            return;

        int count = MacToolbox.ColorTableEntryCount(colorTableHandle);
        for (int i = 0; i <= count; i++)
        {
            int s = i * 3;
            MacToolbox.SetColorTableRGB(colorTableHandle, i,
                (short)((uint)intsBuffer[s] >> 16),
                (short)((uint)intsBuffer[s + 1] >> 16),
                (short)((uint)intsBuffer[s + 2] >> 16));
        }
    }

    // FUN_10071680 — dst[i] -= src[i] over (count+1) Fixed[3] colour-ramp entries.
    public static void Subtract(int[] dstTable, int[] srcTable, int count)
    {
        for (int i = 0; i <= count; i++)
        {
            int e = i * 3;
            dstTable[e] -= srcTable[e];
            dstTable[e + 1] -= srcTable[e + 1];
            dstTable[e + 2] -= srcTable[e + 2];
        }
    }

    // Palette fades. The Mac CLUT-ramp halves (FadeInFromColor/FadeOutToCurrent) are inert in the
    // port's true-colour renderer (gated on ColorQuickDrawAvailable); the visible effect comes
    // from the host ScreenFade* bridge. Kept faithful.

    // FUN_1005d148 — fade the composited frame toward the colour at `targetColorPtr` over
    // `stepCount` steps. ScreenFadeToColor is the host-substrate bridge (the visible effect);
    // FadeInFromColor is the faithful but inert CLUT ramp.
    public static void FadeIn(short stepCount, int targetColorPtr)
    {
        MacToolbox.ScreenFadeToColor(stepCount, targetColorPtr);
        FadeInFromColor(stepCount, targetColorPtr);
    }

    // Managed overload: fade to an explicit Mac RGBColor (16-bit channels), no EvoMemory pointer.
    public static void FadeIn(short stepCount, short red, short green, short blue)
    {
        MacToolbox.ScreenFadeToColor(stepCount, red, green, blue);
        FadeInFromColor(stepCount, red, green, blue);
    }

    // FUN_1005d17c — fade back to the current image over `stepCount` steps. Paired with FadeIn
    // via the faded flag (PaletteState.FadedFlag). ScreenFadeToImage is the host-substrate bridge.
    public static void FadeOut(short stepCount)
    {
        MacToolbox.ScreenFadeToImage(stepCount);
        FadeOutToCurrent(stepCount);
    }

    // FUN_1006fbac — CLUT ramp from the current palette toward `targetColorPtr` (a raw 3-short
    // RGBColor ptr) over `stepCount` steps. Boundary form: reads the RGBColor once and runs the
    // managed overload below.
    public static void FadeInFromColor(int stepCount, int targetColorPtr)
    {
        MacToolbox.ReadRGBColor(targetColorPtr, out short r, out short g, out short b);
        FadeInFromColor(stepCount, r, g, b);
    }

    // Managed overload (explicit RGBColor): the CLUT ramp toward (red,green,blue). Inert in the
    // port (gated on ColorQuickDrawAvailable), kept faithful for the FadeIn tree.
    public static void FadeInFromColor(int stepCount, short red, short green, short blue)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0 || PaletteState.FadedFlag != 0)
            return;

        int gdevice = PaletteState.GDevice;
        int ctHandle = MacToolbox.DeviceColorTable(gdevice);
        int savedSeed = MacToolbox.ColorTableSeed(ctHandle);
        int ctSize = MacToolbox.ColorTableEntryCount(ctHandle);

        int savedDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gdevice);
        int savedHandleState = MacToolbox.HGetState(ctHandle);
        MacToolbox.HNoPurge(ctHandle);
        MacToolbox.HLock(ctHandle);

        // Snapshot the active CTable as Fixed components (channel << 16).
        int[] snapshot = new int[(ctSize + 1) * 3];
        for (int i = 0; i <= ctSize; i++)
        {
            MacToolbox.GetColorTableRGB(ctHandle, i, out short r, out short g, out short b);
            int d = i * 3;
            snapshot[d] = (ushort)r << 16; snapshot[d + 1] = (ushort)g << 16; snapshot[d + 2] = (ushort)b << 16;
        }

        int[] delta = new int[(ctSize + 1) * 3];
        for (int i = 0; i <= ctSize; i++)
        {
            int e = i * 3;
            // Logical shift: the decompile does *(uint*) >> 2; an arithmetic >> 2 would corrupt
            // channels >= 0x8000.
            delta[e] = (int)(((uint)snapshot[e] >> 2) + (uint)((ushort)red * -0x4000)) / stepCount << 2;
            delta[e + 1] = (int)(((uint)snapshot[e + 1] >> 2) + (uint)((ushort)green * -0x4000)) / stepCount << 2;
            delta[e + 2] = (int)(((uint)snapshot[e + 2] >> 2) + (uint)((ushort)blue * -0x4000)) / stepCount << 2;
        }

        for (int i = 0; i < stepCount; i++)
        {
            Subtract(snapshot, delta, ctSize);
            IntsBufferToColorTable(snapshot, ctHandle);
            MacToolbox.SetColorTableEntries(ctHandle, 0, ctSize);
        }
        MacToolbox.HSetState(ctHandle, (byte)savedHandleState);
        MacToolbox.SetColorTableSeed(ctHandle, savedSeed);
        MacToolbox.CopyColorTableRGB(ctHandle, PaletteState.SavedCTab, ctSize);  // restore from saved

        PaletteState.FadedFlag = 1;
        PaletteState.FadedRed = red;
        PaletteState.FadedGreen = green;
        PaletteState.FadedBlue = blue;
        MacToolbox.SetGDevice(savedDevice);
    }

    // FUN_1006fe50 — CLUT ramp from the saved fade colour back to the current palette over
    // `stepCount` steps; clears the faded flag. (Decompile 45741-45813.)
    public static void FadeOutToCurrent(int stepCount)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0 || PaletteState.FadedFlag == 0)
            return;

        int gdevice = PaletteState.GDevice;
        int savedCTab = PaletteState.SavedCTab;
        int ctHandle = MacToolbox.DeviceColorTable(gdevice);
        int savedSeed = MacToolbox.ColorTableSeed(ctHandle);
        int ctSize = MacToolbox.ColorTableEntryCount(ctHandle);
        // Fill every entry with the saved fade colour.
        for (int i = 0; i <= ctSize; i++)
            MacToolbox.SetColorTableRGB(ctHandle, i,
                PaletteState.FadedRed, PaletteState.FadedGreen, PaletteState.FadedBlue);

        int[] fromSnapshot = ColorTableToIntsBuffer.Run(savedCTab);
        if (fromSnapshot == null)
            return;
        int[] toSnapshot = ColorTableToIntsBuffer.Run(ctHandle);
        if (toSnapshot == null)
            return;
        int[] delta = new int[768];   // 256 colours x 3 Fixed components
        for (int i = 0; i <= ctSize; i++)
        {
            int r = i * 3, g = r + 1, b = r + 2;
            // Logical shift: the decompile does *(uint*) >> 2; an arithmetic >> 2 would corrupt
            // channels >= 0x8000.
            delta[r] = (int)(((uint)toSnapshot[r] >> 2) - ((uint)fromSnapshot[r] >> 2)) / stepCount << 2;
            delta[g] = (int)(((uint)toSnapshot[g] >> 2) - ((uint)fromSnapshot[g] >> 2)) / stepCount << 2;
            delta[b] = (int)(((uint)toSnapshot[b] >> 2) - ((uint)fromSnapshot[b] >> 2)) / stepCount << 2;
        }
        int savedDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gdevice);
        int savedHandleState = MacToolbox.HGetState(ctHandle);
        MacToolbox.HNoPurge(ctHandle);
        MacToolbox.HLock(ctHandle);
        for (int i = 0; i < stepCount; i++)
        {
            Subtract(toSnapshot, delta, ctSize);
            IntsBufferToColorTable(toSnapshot, ctHandle);
            MacToolbox.SetColorTableEntries(ctHandle, 0, ctSize);
        }
        MacToolbox.HSetState(ctHandle, (byte)savedHandleState);
        MacToolbox.SetColorTableSeed(ctHandle, savedSeed);
        PaletteState.FadedFlag = 0;
        MacToolbox.SetGDevice(savedDevice);
    }

    // FUN_1006fa34 — re-apply the saved GWorld palette and repaint behind the front window
    // (after a window activate / depth change).
    public static void ReapplySaved()
    {
        if (RenderGlobals.ColorQuickDrawAvailable != 0)
        {
            RestoreGWorldPalette(1);
            PaintBehindFrontWindow.Run();
        }
    }

    // FUN_100713c0 — repaint every window behind the backmost window (after a palette change),
    // restore the saved port, and redraw the menu bar.
    public static void RepaintBehindFrontWindow()
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int region = MacToolbox.NewRgn();
        MacToolbox.RectRgn(region, MacToolbox.DeviceBoundsRect(PaletteState.GDevice));

        // Walk the window list to the backmost window.
        int backWindow = MacToolbox.FrontWindow();
        while (backWindow != 0 && MacToolbox.NextWindow(backWindow) != 0)
            backWindow = MacToolbox.NextWindow(backWindow);

        int[] savedPort = new int[3];
        MacToolbox.GetPort(savedPort);
        MacToolbox.CalcVisBehind(backWindow, region);
        MacToolbox.PaintBehind(backWindow, region);
        MacToolbox.SetPort(savedPort[0]);
        MacToolbox.DisposeRgn(region);
        MacToolbox.DrawMenuBar();
    }

    // FUN_1005d2c4 — reinstall the default/base palette (no SetEntries — stages the table
    // without touching the visible hardware CLUT). The decompile passes *_DAT_10080f68, the
    // default screen-palette handle (ScreenPaletteCTab).
    public static void RestoreDefault()
    {
        InstallScreenPalette(ScreenPaletteCTab, 0);
    }

    // FUN_100416f4 — fade the screen back in and clear (paint black) the main game window's port
    // rect. LIVE: called at boot (GameBootSequence.cs:162). (ScreenFadeCTab is never seeded, so the
    // FadeIn resolves to black — the boot screen clear.)
    public static void ResetFadeAndClearRegion()
    {
        FadeIn(16, ScreenFadeCTab);   // ScreenFadeCTab never seeded -> black
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
    }

    // FUN_1007126c — copy the saved-snapshot palette back into the active GDevice colour table;
    // reseed from the snapshot seed.
    public static void RestoreGWorldPalette(byte applyEntries)
        => RestorePaletteCore(applyEntries, PaletteState.SnapshotCTab, PaletteState.SnapshotSeed, snapshot: false);

    // FUN_10071108 — same, from the saved-palette slot, reseed from the saved seed, then
    // snapshot the result.
    public static void RestorePaletteFromSaved(byte applyEntries)
        => RestorePaletteCore(applyEntries, PaletteState.SavedCTab, PaletteState.SavedSeed, snapshot: true);

    // Shared core of the two restore-from-snapshot paths: lock the active colour table, copy RGB
    // entries from the `srcCtHandle` palette, optionally SetEntries, reseed to `seed`, and
    // (optionally) snapshot.
    private static void RestorePaletteCore(byte applyEntries, int srcCtHandle, int seed, bool snapshot)
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        int gdevice = PaletteState.GDevice;
        int destCTab = MacToolbox.DeviceColorTable(gdevice);
        int savedDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gdevice);
        int savedHandleState = MacToolbox.HGetState(destCTab);
        MacToolbox.HNoPurge(destCTab);
        MacToolbox.HLock(destCTab);

        int entryCount = MacToolbox.ColorTableEntryCount(destCTab);
        MacToolbox.CopyColorTableRGB(destCTab, srcCtHandle, entryCount);

        if (applyEntries != 0)
            MacToolbox.SetColorTableEntries(destCTab, 0, entryCount);
        MacToolbox.HSetState(destCTab, (byte)savedHandleState);
        MacToolbox.MakeITable(0, 0, 0);
        MacToolbox.SetColorTableSeed(destCTab, seed);
        if (snapshot)
            SnapshotCTable(PaletteState.SavedCTab, out PaletteState.SavedSeed);
        MacToolbox.SetGDevice(savedDevice);
    }

    // FUN_1005d2f4 — force the radar / shield / armour HUD colours to white (cloak-engaged visual).
    public static void SetHudColorsWhite()
    {
        UiColors.Radar = UiColors.Friendly = UiColors.Neutral = UiColorConstants.HudColorCloakWhite;
    }

    // FUN_1005d358 — restore the active HUD theme (Mac 16-bit greens -> packed 0xRRGGBB).
    public static void SetHudColorsActive()
    {
        UiColors.Radar = UiColorConstants.HudColorActiveRadar;
        UiColors.Friendly = UiColorConstants.HudColorActiveFriendly;
        UiColors.Neutral = UiColorConstants.HudColorActiveNeutral;
    }

    // FUN_10052a3c (EV Override-11.c 33886) — seed the HUD/dialog/galaxy colour globals with
    // their Mac defaults (16-bit RGBColor -> packed 0xRRGGBB high byte). LIVE: the original reached
    // it via FUN_10052b38 (unported), but the port calls it DIRECTLY at boot (GameBootSequence.cs:70),
    // so the HUD colours ARE seeded every boot (that's why the HUD renders in colour and chatter
    // isn't black-on-black in real play). Of the decompile's two pointer-target writes: ChatterText
    // MUST be seeded white here (line below) or chatter draws black-on-black over blackColor and is
    // invisible; the screen-fade CTabHandle write is not a colour and is dropped (the port's
    // brightness fade ignores it).
    public static void InitHudColors()
    {
        UiColors.AuxGreen = UiColorConstants.HudColorAuxGreenSeed;
        UiColors.Friendly = UiColorConstants.HudColorFriendlySeed;
        UiColors.Radar = UiColorConstants.HudColorRadarSeed;
        UiColors.Neutral = UiColorConstants.HudColorNeutralSeed;
        UiColors.Frame = UiColorConstants.HudColorFrameSeed;
        UiColors.DialogFore = UiColorConstants.HudColorDialogForeSeed;
        UiColors.Unexplored = UiColorConstants.HudColorUnexploredSeed;
        UiColors.OutfitFrame = UiColorConstants.HudColorOutfitFrameSeed;
        UiColors.ChatterText = UiColorConstants.HudColorChatterTextSeed;   // white, else chatter is black-on-black
    }
}
