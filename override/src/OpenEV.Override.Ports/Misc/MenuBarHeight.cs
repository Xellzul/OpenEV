using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 54550-54554.
//
// Reads the Mac low-memory global 0x0BAA (MBarHeight, signed, in pixels) via the
// same LMGetMBarHeight accessor HideMacMenuBar/RestoreMacMenuBar use to save/
// restore it. NO-OP: currently always 0 because LMGetMBarHeight is an unwired
// stub; in the original game it was also always 0 here, since HideMacMenuBar
// zeroes the global long before this ever runs (the port's menu bar stays
// permanently hidden).
public static class MenuBarHeight
{
    public static int Run()
    {
        return MacToolbox.LMGetMBarHeight();
    }
}
