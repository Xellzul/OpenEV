namespace OpenEV.Override.Ports.Core.Model;

// Managed homes for the game-window / galaxy-map setup cells InitGalaxyMapWindow
// touches — were raw data-segment cells the old EvoMemory boundary poked through
// directly (EvoMemory itself was later removed). Migrated to plain managed fields.
public static class GameWindowGlobals
{
    // Managed window globals (were the ptr cells 0x100811c0/0x100811bc/0x100811b8
    // and the GDHandle cell 0x10081100 + the heap Rect records behind them).
    public static int GameWindowPtr;                                  // the NewCWindow token

    // {top,left,bottom,right} — seeded once at boot from the host main device.
    public static readonly short[] GameWindowBounds = new short[4];

    // Written by InitGalaxyMapWindow; no reader yet (kept for fidelity).
    public static readonly short[] GalaxyMapRect = new short[4];

    // The screen GDevice handle TitleMemory.Init stores — the managed MacGDevices
    // handle InitMainScreenDevice returns directly (was a NewPtr(4) GDHandle cell
    // *_DAT_10081100 derefed once; the indirection is gone, so no EvoMemory read).
    public static int ScreenGDeviceHandle;

    // The PEF-relocated window-source token (was *0x100811b4 = toc-0x74ac; raw
    // 0x1f28 + dataBase 0x10080660, dumped — the EvoMemory read this cell used to
    // need is gone now that the value is a literal). SetCurrentGameWindow stores it
    // into GlobalScratchHandle below (whose only deref, FatalOOM's UPP, is
    // documented-0), so the concrete value is observationally inert.
    public const int CurrentWindowSource = 0x10082588;

    // SetCurrentGameWindow stores the current-window token here (was the cell 0x10081a9c);
    // FatalOutOfMemoryExit reads it with a SINGLE deref and invokes the non-zero token as a
    // UPP (the error-handler callback).
    public static int GlobalScratchHandle;

    public static short[] GameWindowBoundsRect() => GameWindowBounds;

    public static void SetMenuBarHidden(bool hidden)
        => Core.Model.WorldState.MenuBarHidden = (byte)(hidden ? 1 : 0);

    public static void SetGalaxyMapRect(short top, short left, short bottom, short right)
    {
        GalaxyMapRect[0] = top;
        GalaxyMapRect[1] = left;
        GalaxyMapRect[2] = bottom;
        GalaxyMapRect[3] = right;
    }
}
