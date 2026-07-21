namespace OpenEV.Override.Ports.Dialog.Model;

// Managed home for the MISSION-BOARD dialog globals — shared by the mission BBS
// (FUN_10047814, DLOG 1006) and the single-mission offer dialog that reuses the
// same window/pict cells.
public static class MissionBoardGlobals
{
    // *0x10080c4c (PTR_DAT_10080c4c, ptr -> int cell): the open mission-board
    // window — the BBS list dialog or the single-mission offer dialog (only one
    // is open at a time; several dialogs gate on "== 0" = neither open).
    public static int DialogWindow;

    // *0x10080c48: 2 {normal, pressed} PICT pairs of the board's button row
    // (0x1b8a/0x1b8b accept, 0x1b60/0x1b61 leave).
    public static readonly int[] Picts = new int[4];

    // *0x1008113c (toc-0x7524, ptr -> cell): the BBS missions ListHandle
    // (the port's List Manager is a stub — LNew returns 0).
    public static int BbsListHandle;

    // BBS modal-filter proc key (was UPP source cell 0x10081148 -> FUN_1004cad0).
    public const int BbsFilterProc = 0x1004cad0;

    // Single-mission OFFER modal-filter proc key (was UPP source cell
    // 0x10081124 -> FUN_100513e4 = Dialog.ConfirmDialogFilter).
    public const int OfferFilterProc = 0x100513e4;

    // *0x10080c38 (toc-0x7a28, ptr -> byte): the single-mission OFFER dialog's
    // button-row layout — 1 = accept/refuse PICT pair on items 1/2 (normal
    // mission, 'mïsn' flags bit 2 clear; PICTs 0x1bc4..0x1bc7), 0 = a single
    // OK button on item 6 (flags bit 2 set; PICTs 0x1b8e/0x1b8f, Picts[2..3]
    // zeroed). Written by RunSingleMissionDialog; read by RenderSingleMission-
    // ButtonRow / TrackSingleMissionButtonMouseDown and ConfirmDialogFilter
    // ('n' = refuse only when a refuse button exists).
    public static byte OfferAcceptRefuseLayout;

    // 0x1008114c (toc-0x7514): ptr -> the mission NAME table (Pascal strings,
    // stride 0x100, indexed by 'bär' person id). Heap-table boundary — read at
    // sites via PascalToString(NameTable + pers * 0x100).
    // Managed: the per-mission display-name cache (was the BSS Pascal table,
    // stride 0x100, behind ptr cell 0x1008114c; filled from GetResInfo by
    // LoadBarPersonResources).
    public static readonly string[] Names = new string[0x200];
}
