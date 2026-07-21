using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Misc;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Boot;

// Decompile: EV Override-11.c lines 40131-40190.
//
// Boot step 8. On System 7.5+ the original wanted to show a one-button warning
// dialog (DLOG 0xbc2) — but only when FUN_100600f4 returns 0, and that helper is a
// `return 1;` stub in the binary, so the dialog is DEAD on every platform (Mac
// included); on 7.5+ the function merely latches the warning flag to 1. The port also
// models the host as System 7.0 (the SysEnvirons shim no-ops), so the outer 7.5+
// gate is false too. The full structure is kept for fidelity.
public static class ShowOldOsWarningIfNeeded
{
    private const int WarningDialogId = 0xbc2;  // DLOG 3010 — "old OS" warning
    private const int ItemContinue = 1;      // OK / continue
    private const int ItemDontWarnAgain = 2;      // clears the warning flag

    public static void Run()
    {
        // SysEnvirons fills SysEnvRec.systemVersion (+4); the shim no-ops, so model
        // the host as System 7.0 (0x700) — same as SystemVersionCheck. The gate is
        // `0x74f < systemVersion`, i.e. System 7.5+, so at 0x700 it is false.
        short systemVersion = 0x700;
        MacToolbox.SysEnvirons(2, 0);
        if (systemVersion <= 0x74f) return;   // not System 7.5+ — nothing to warn about

        // First time on 7.5+: latch the flag (FUN_100600f4 is the always-1 stub).
        if (!SystemGlobals.OldOsWarningAcknowledged)
        {
            if (junkcode.FUN_100600f4() != 0)
                SystemGlobals.OldOsWarningAcknowledged = true;
            return;
        }

        // Flag already set: re-check the helper and show DLOG 0xbc2 if it returns 0.
        // Dead (helper always returns 1); preserved verbatim.
        if (junkcode.FUN_100600f4() != 0) return;

        SystemGlobals.OldOsWarningDialog = 0;
        int dlg = MacToolbox.GetNewDialog(WarningDialogId, 0, -1);
        SystemGlobals.OldOsWarningDialog = dlg;
        if (dlg == 0) return;

        MacToolbox.SetPort(dlg);
        MacToolbox.SetDialogDefaultItem(dlg, ItemContinue);
        MacToolbox.ShowWindow(dlg);
        MacToolbox.SetPort(dlg);
        MacToolbox.SelectWindow(dlg);
        MacToolbox.SetPort(dlg);
        MacToolbox.DrawDialog(dlg);
        DrawDefaultButtonOutline.Run(dlg, ItemContinue);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);

        // Decompile's local_1c[8] only ever reads index [0]; modeled as a single short.
        // ModalDialog's filterProc arg is 0 (no filter), matching the decompile.
        short itemHit = 0;
        bool done = false;
        do
        {
            MacToolbox.ModalDialog(0, ref itemHit);
            if (itemHit == ItemContinue) done = true;
            if (itemHit == ItemDontWarnAgain)
            {
                SystemGlobals.OldOsWarningAcknowledged = false;   // "don't warn again" — user picked item 2
                done = true;
            }
        } while (!done);

        MacToolbox.HideWindow(dlg);
        MacToolbox.DisposeDialog(dlg);
    }
}
