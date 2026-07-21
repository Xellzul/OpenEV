using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 51267-51307.
//
// Fatal out-of-memory path: tear down the global GWorlds, show (or hand to a custom error
// handler) the out-of-memory alert, shut down sound, and ExitToShell.
//
// DEVIATION (faithful): `message` is a C# string. The original took the data-seg Pascal-string
// ADDRESS and copied its bytes into a 256-byte local stack buffer — that buffer fed
// RunMultiButtonModalDialog, whose NewDialog is still a stub returning 0 (see that file), so the
// alert window never opens and the copied bytes are never read; the copy is dropped as dead
// machinery, not a content change. The only remaining boundary read is the global scratch Handle
// (a Mac global) that optionally holds a custom error-handler UPP.
public static class FatalOutOfMemoryExit
{
    public static void Run(string message)
    {
        TeardownGlobalGWorlds.Run();

        // Read the live cell — do not hardcode 0. GameWindowGlobals.GlobalScratchHandle mirrors
        // *_DAT_10081a9c (single deref = the stored token value); GWorldPort.SetCurrentGameWindow
        // sets it non-zero during galaxy-map init (window-source token 0x10082588), so the else
        // branch below is reachable once that has run. Invoking a data token as a UPP is an
        // original-game quirk kept for parity; InvokeUpp1Arg resolves the one known dispatch
        // target for it (TeardownAudioForExit) — see that file.
        int errorHandlerUpp = GameWindowGlobals.GlobalScratchHandle;
        if (errorHandlerUpp == 0)
        {
            // Single-button alert (decompile: empty paramText 0x10082464 x3, button title from
            // data-seg cell 0x1008584c = StaticData.UiErrorStrings[FatalAlertButtonIndex], seeded
            // "Quit" and refreshed from STR# 25000). NewDialog is still a stub returning 0 (see
            // RunMultiButtonModalDialog's own NO-OP note), so this call's dialog==0 branch always
            // fires: FatalAlertExit beeps and calls ExitToShell (real Environment.Exit) immediately
            // — the alert is never shown, and this function's own ExitToShell below is unreached here.
            RunMultiButtonModalDialog.Run(message, "", "", "", 1, 1, 0,
                                          StaticData.UiErrorStrings[StaticData.FatalAlertButtonIndex]);
        }
        else
        {
            InvokeUpp1Arg.Run(errorHandlerUpp);
        }

        SoundSubsystemShutdown.Run();
        MacToolbox.ExitToShell();
    }
}
