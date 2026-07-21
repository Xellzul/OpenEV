using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_10071b24 (EV Override-11.c lines 46761-46883) — the shareware nag (DLOG 900):
// shown when the build is shareware-flagged, unregistered and 7+ days installed
// (or forceShow). Installs userItem draw procs on items 3/5 and the
// DefaultDialogFilter modal filter, loads the styled TEXT 900 into item 5 (plus
// TEXT 0x385 into item 6 once the escalation level is positive), zoom-animates
// the window in, stamps the filter's hold-off via SetWRefCon(escalation*300 +
// TickCount), then loops ModalDialog until item 1 ("Register" — launches the
// Register app) or item 2 (dismiss). Called live from TitleMainLoop.RunSetupOnce
// with forceShow=1 on every title visit (CheckShareWareRegistrationMatch always
// reads unregistered in the port — no REG resource).
public static class ShowSharewareNagDialog
{
    // Proc keys for the resolved UPP cells (PEF TVector follow; the FUN code
    // addresses double as registry keys, NewRoutineDescriptor echoes them). These
    // are GameToc data-seg globals — read via the GameToc base, not the
    // decompile's unassigned r2/RTOC.
    private const int Item3DrawProc = 0x10073418;        // _DAT_100819cc (toc-0x6c94) = Dialog.DefaultButtonOutline, item-3 userItem proc
    private const int Item5DrawProc = 0x100733f4;        // 0x100819c8 (toc-0x6c98) = Dialog.UpdateAllTextEditsTrampoline, item-5 userItem proc
    private const int NagFilterProc = 0x1007328c;        // 0x100819c4 (toc-0x6c9c) = Dialog.DefaultDialogFilter, modal filter (typed)
    private const int StyledTextCallback = 0x10072eac;   // _DAT_100819c0 (toc-0x6ca0) = Misc.ResolveShareWarePlaceholder, styled-text ^N callback UPP

    // The registration-app name Str255 the decompile copies out of the PEF
    // data-seg record at GameToc-0x37fa (copy loop fills local_144 = the Pascal
    // string at 0x10084e6e; dumped: len 9, "Register " with a trailing space).
    private const string RegisterAppNamePrefix = "Register ";

    // Port bridge for the modal-filter UPP (typed shape; the filter's item-hit out
    // lands in _filterHit — the core loop reads only the consumed flag).
    private static short _filterHit;
    private static int FilterAdapter(int dialog, MacEvent evt)
        => DefaultDialogFilter.Run(dialog, evt, ref _filterHit);

    public static int Run(bool forceShow)
    {
        int result = 0;
        // Decompile: `*_DAT_10081268 == '\0'` — the registration-session-open flag,
        // managed ShareWareGlobals.Registered now.
        if (ShareWareGlobals.Registered == 0)
        {
            result = -1001;   // 0xfffffc17 — "not a shareware build" sentinel
        }
        else
        {
            int daysSinceInstall = 7;    // local_2c
            CheckShareWareRegistrationMatch.Run(out byte registeredFlag);
            GetDaysSinceInstall.Run(out daysSinceInstall);
            if ((registeredFlag == 0 && 6 < daysSinceInstall) || forceShow)
            {
                bool done = false;
                short escalation = (short)EscalationLevel.Run();
                // The three NewRoutineDescriptor calls on the (resolved) UPP cells;
                // the modal filter is registered typed under the same key.
                int okProc = MacToolbox.NewRoutineDescriptor(Item3DrawProc, 0x2c0, 1);
                int proc5 = MacToolbox.NewRoutineDescriptor(Item5DrawProc, 0x2c0, 1);
                int modalProc = MacToolbox.NewRoutineDescriptor(NagFilterProc, 0xfd0, 1);
                MacToolbox.RegisterModalFilter(NagFilterProc, FilterAdapter);
                MacToolbox.InitCursor();
                if (MacToolbox.GetResource(MacResType.Dialog, 900) == 0)
                {
                    MacToolbox.ExitToShell();
                }
                if (MacToolbox.GetResource(MacResType.DialogItemList, 900) == 0)
                {
                    MacToolbox.ExitToShell();
                }
                int dialog = MacToolbox.GetNewDialog(900, 0, -1);
                var itemType = new short[1];
                var itemHandle = new int[1];
                var itemRect = new short[4];   // also the styled-text dest rect below
                MacToolbox.GetDialogItem(dialog, 2, itemType, itemHandle, itemRect);
                MacToolbox.HiliteControl(itemHandle[0], 0xff);
                // Install the userItem draw procs on items 3/5.
                // NO-OP: SetDialogItem is an unwired no-op shim — the draw procs never
                // actually attach, so DLOG 900's custom item art stays unrendered.
                MacToolbox.GetDialogItem(dialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.SetDialogItem(dialog, 3, itemType[0], okProc, itemRect);
                MacToolbox.GetDialogItem(dialog, 5, itemType, itemHandle, itemRect);
                MacToolbox.SetDialogItem(dialog, 5, itemType[0], proc5, itemRect);
                var savedPort = new int[1];   // GetPort save (shim writes 0 → screen fallback on restore)
                MacToolbox.GetPort(savedPort);
                MacToolbox.SetPort(dialog);
                // FUN_10073690(900, &item5Rect, _DAT_100819c0) — styled TEXT 900 into the
                // item-5 rect; the callback UPP is the resolved proc key now.
                LoadStyledTextResource.Run(900, itemRect, StyledTextCallback);
                if (0 < escalation)
                {
                    MacToolbox.GetDialogItem(dialog, 6, itemType, itemHandle, itemRect);
                    LoadStyledTextResource.Run(901, itemRect, StyledTextCallback);   // TEXT 0x385
                }
                MacToolbox.SetPort(savedPort[0]);
                ZoomInWindowAnimation.Run(dialog);
                int nowTicks = (int)MacToolbox.TickCount();
                MacToolbox.SetWRefCon(dialog, escalation * 300 + nowTicks);
                short itemHit = 0;
                while (!done)
                {
                    MacToolbox.ModalDialog(modalProc, ref itemHit);
                    if (itemHit < 3 && 0 < itemHit)
                    {
                        done = true;
                    }
                }
                MacToolbox.HideWindow(dialog);
                DisposeAllTextEditList.Run();
                MacToolbox.DisposeRoutineDescriptor(modalProc);
                MacToolbox.DisposeDialog(dialog);
                MacToolbox.DisposeRoutineDescriptor(okProc);
                MacToolbox.DisposeRoutineDescriptor(proc5);
                if (itemHit == 1)
                {
                    // Registration-app name: the decompile copies the data-seg record at
                    // GameToc-0x37fa into a stack overlay (32 pair-writes filling
                    // local_144 = "Register ", dumped literal above), then BlockMoves
                    // GetIndString(900,1)'s chars onto its tail and bumps the length
                    // byte — i.e. a Pascal append. C# string concat now.
                    string regAppName = RegisterAppNamePrefix + MacToolbox.GetIndString(900, 1);
                    short launchErr = (short)LaunchApplicationByFSSpec.Run(regAppName, 1);
                    if (launchErr != 0)
                    {
                        var delayTicks = new int[1];   // auStack_348 — Delay elapsed-ticks out (shim ignores it)
                        for (short beep = 0; beep < 5; beep = (short)(beep + 1))
                        {
                            MacToolbox.SysBeep(1);
                            MacToolbox.Delay(60, delayTicks);   // 0x3c ticks = 1 second
                        }
                        MacToolbox.InitCursor();
                        // GetIndString(900,4) -> ParamText ^0/^2/^3; the app name -> ^1.
                        string couldNotLaunch = MacToolbox.GetIndString(900, 4);
                        MacToolbox.ParamText(couldNotLaunch, regAppName, couldNotLaunch, couldNotLaunch);
                        short alertHit = MacToolbox.Alert(901, 0);   // ALRT 0x385
                        if (alertHit == -1)
                        {
                            MacToolbox.ExitToShell();
                        }
                    }
                }
            }
        }
        return result;
    }
}
