namespace OpenEV.Override.Ports.Dialog.Model;

// Managed home for the player-info dialog globals (the FUN_1003eda8 family:
// launcher + filter + RenderPlayerInfoDialog/TabRow).
public static class PlayerInfoGlobals
{
    // *0x10080c34 (ptr -> int cell): the open player-info dialog window.
    public static int DialogWindow;

    // *0x10080c30: 10-entry PICT-handle array; only 8 are loaded (the
    // {normal, pressed} tab pairs 0x1b64/65, 0x1b7e/7f, 0x1b84/85, 0x1b86/87)
    // but the purge loop walks all 10 — original-game bug kept.
    public static readonly int[] Picts = new int[10];

    // Modal-filter proc key (was UPP source cell 0x10080fd8 -> FUN_1003f044).
    public const int FilterProc = 0x1003f044;
}
