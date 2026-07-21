using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// Set Prefs support wiring (dialog 4-rules campaign rewrite).
//
// Installs the pre-boot defaults + resources the Set Prefs dialog family needs
// (game-speed default, volume labels, default keymap) and registers the keybind
// modal filter + the Game Speed slider redraw. The old ptr-cell plumbing
// (0x100810c4/c8/cc/d4/dc/e0/e4, toc-0x7590/-0x758c/-0x7834 and their
// 0x1020xxxx scratch backing) is GONE — state lives in PrefsDialogState.
//
// The actual saved-prefs DISK LOAD is NOT done here: the boot sequence runs the
// real FUN_10019f88 port (ApplyDefaultPrefsToMemory) at step 7, after this Init.
public static class PrefsMemory
{
    // ── Toolbox boundary staging (KEPT) ───────────────────────────────
    // The prefs-disk FSSpec is walked BY ADDRESS by the File Manager traps
    // (FSMakeFSSpec/FSpCreateResFile/...) — same sanctioned boundary as the
    // DialogScratch text buffers. Shared by WritePrefsToDisk +
    // ApplyDefaultPrefsToMemory (one-shot, never concurrent).
    // (WritePrefs_FileName Str255 staging is gone: the prefs filename comes from
    //  the managed GetIndString(0x82,7) → string form, no EvoMemory buffer.)
    public const int WritePrefs_FSSpec = 0x10202200;  // 70 B FSSpec record (auStack_174)

    private static bool _inited;

    public static void Init()
    {
        if (_inited) return;
        _inited = true;

        // Default game speed = 1.0 (100%) until a saved pref overrides it, so a
        // fresh WritePrefsToDisk records 100 rather than 0.
        PrefsDialogState.GameSpeed = 1.0;

        // Sound Volume labels: STR# 136 (0x88), 9 entries (levels 0..8). The
        // prefs redraw SetDialogItemText's item 0x25 to VolumeLabels[volume].
        for (int i = 0; i < PrefsDialogState.VolumeLabels.Length; i++)
            PrefsDialogState.VolumeLabels[i] = MacToolbox.GetIndString(0x88, (short)(i + 1));

        // ── Volume-preview sound handle ───────────────────────────────
        // DEVIATION (faithful): the volume up/down buttons play the snd at *0x1008a5d0 at the new
        // level so you can hear it. The real UI-sound loader (FUN_10052d68, ported as
        // Sound.LoadAllUiSoundEffects, boot step 25) fills all of UiSoundBankA, but this Init runs
        // on the main thread in TitleAdapter.Setup BEFORE the title thread spawns and runs
        // GameBootSequence.RunPreTitle (which is where step 25 lives) — so the cell is genuinely
        // unset yet at this point. Wire just the one cell this dialog needs ahead of that load:
        // FUN_10052d68 loads it as the 5th of the 0x1008a5c0 group = FUN_10075450(0x96+4) = snd 154
        // (0x9a).
        Sound.Model.CombatSoundCells.UiSoundBankA[4] = MacToolbox.MakeSndHandle(0x9a);

        // ── Default key bindings ──────────────────────────────────────
        // Populate the PrefsRecord keymap (0x1008a558+) with the Mac
        // defaults. PrefsDialogInit then calls PackKeyBindings to permute
        // PrefsRecord → LIVE for display/editing. This is a pre-boot default;
        // the boot prefs-load (step 7) re-installs/overrides the keymap.
        Misc.Model.Keymap.InitDefaultMacKeyBindings();

        // ── Saved prefs (persistence) ─────────────────────────────────
        // The disk load is no longer done here: the boot sequence runs the
        // real FUN_10019f88 port (ApplyDefaultPrefsToMemory) at step 7, AFTER
        // this Init (Init runs in V2TitleAdapter.Setup, before the title thread
        // drives RunPreTitle). So the defaults installed above are overwritten
        // by the saved 'Mp¨Ä' blob on the happy path / the fallback defaults on
        // a missing file — through the original func, not a bespoke shortcut.

        // ── Modal filter + slider redraw registration ─────────────────
        // Typed (MacEvent) filter registration; method groups, no lambdas.
        MacToolbox.RegisterModalFilter(PrefsDialogState.KeyAssignFilterProc,
                                       HandleKeyAssignDialogEvent.Run);

        // Game Speed dialog (DLOG 4002) has no modal filter, so its slider
        // (item 4, drawn by FUN_10045504) would be erased by ModalDialog's
        // background fill. Register the slider redraw as the dialog's UserItem
        // draw so it is repainted after each fill.
        MacToolbox.RegisterDialogUserDraw(0xfa2, Title.DrawGameSpeedSlider.Run);
    }
}
