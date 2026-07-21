using System;

namespace OpenEV.Override.Ports.Dialog.Model;

// Managed home for the spaceport-dialog globals (the FUN_10036e74 family: main
// spaceport hub + tab bar + modal filter + redraw + sub-dialogs). Each field
// replaces a PEF ptr-cell AND the BSS target it pointed at:
//   0x10080ba0 (toc-0x7ac0)  window-ptr cell -> DialogWindow
//   0x10080be4 (toc-0x7a7c)  escort-mode short -> Outfit.Model.ShipyardState.EscortMode
//   0x10080c74 (toc-0x79ec)  PICT-handle array -> TabPicts
//   0x10080c78 (toc-0x79e8)  BBS-enabled byte -> MissionBbsEnabled
//   0x1008103c..0x10081058 (toc-0x7624..-0x7608) the spaceport cluster below
//   0x1008a504 (toc+0x1ea4)  current-spob ptr -> Systems.Model.CurrentSpob.Index
public static class SpaceportGlobals
{
    // *0x10080ba0: the open spaceport-family dialog window (shared by the hub and
    // every sub-dialog that re-uses the cell while nested).
    public static int DialogWindow;

    // *0x10080c74: 7 {normal, pressed} PICT-handle pairs for the tab bar
    // (PICTs 0x1b66..0x1b6f, 0x1b78/0x1b79, 0x1bb2/0x1bb3). Tab i draws
    // TabPicts[i*2], or TabPicts[i*2+1] while pressed.
    public static readonly int[] TabPicts = new int[14];

    // *0x10081038 (toc-0x7628): the current spob picture Handle (item 5 art).
    public static int SpobPictHandle;

    // *0x10081044 (toc-0x761c): PICT id of the spob picture (SpriteId+10000, or
    // the spob's CustomPicId when >= 0x80).
    public static short SpobPictId;

    // *0x1008104c (toc-0x7614): the spob description text. Was a C-string buffer
    // (strncpy ""/LoadDescriptionText filled it, TETextBox drew strlen of it);
    // managed C# string now.
    public static string Description = "";

    // *0x10081040 (toc-0x7620): decoded ambient 'snd ' Handle for the spob's
    // CustomSoundId (0 = none). Played on item-5 clicks and by the modal filter's
    // ambient countdown; FlushMixQueueEntries + DisposePtr at dialog exit.
    public static int AmbientSndHandle;

    // *0x1008103c (toc-0x7624): ambient-sound re-trigger countdown (the filter
    // decrements per event; at 0 plays the ambient and rearms to rng(0x200)+0x200).
    public static short AmbientTimer;

    // *0x10081048 (toc-0x7618): float[2] shop price scale, seeded at spaceport
    // entry from the toc-0x6610 default word (0x10082050). The shipyard's
    // PriceQuantize calls read [0]/[1] (and pass them as the decompile-dropped,
    // unused f1 lead arg).
    public static readonly float[] ShopPriceScale = new float[2];
    public static float DefaultShopPriceScale => BitConverter.Int32BitsToSingle(0x3f8fced9); // ~1.1235

    // *0x10081050 (toc-0x7610): set when a ship purchase commits; spaceport exit
    // runs 4 TickWorldDailyEvents and clears the player's target.
    public static byte ShipPurchased;

    // *0x10081054 (toc-0x760c): set on any outfitter buy/sell; spaceport exit
    // runs 1 TickWorldDailyEvents and clears the player's target.
    public static byte LoadoutChanged;

    // *0x10080c78 (toc-0x79e8): mission-BBS tab enabled — 1 when SpobFlags.Uninhabited
    // is CLEAR (set by the hub on entry and re-derived by the modal filter every event).
    public static byte MissionBbsEnabled;

    // Modal-filter proc key for RegisterModalFilter/NewRoutineDescriptor — the
    // code address of FUN_100377d8 (SpaceportFilter), which the original
    // kept PEF-relocated in the UPP source cell 0x10081058.
    public const int FilterProc = 0x100377d8;

    // ── Bar / mission-BBS shared state ─────────────────────────────────
    // *0x10080c0c (toc-0x7a54): the spob the BBS/bar availability tables were
    // last generated for; -1 forces a regenerate on next open.
    public static short BbsLastSpob;

    // *0x10080c14 (toc-0x7a4c): mode flag — 1 = in the BAR, 0 = mission BBS.
    // Indexes MissionAvailGrid.ByMode; gates bar-person eligibility (a 'bär'
    // person's Field0x06: 0 = BBS-only, 1 = bar-only); the availability refresh
    // sweeps both modes through it and restores the saved value.
    public static short InBarFlag;

    // The bar news terminal's two text lines (DLOG 1014, PICT 9000), built by
    // Text.BuildBarNewsText from STR# 8100/8101 or a cron price-news event.
    // Were the C-string buffers 0x100865a0 / 0x100861a0.
    public static string BarNewsLineA = "";
    public static string BarNewsLineB = "";

    // The bar's working queue of available 'bär' persons (indices into
    // MissionAvailTable), priority (flags&0x1000) entries first; compacted as each
    // person departs. Was the BSS short[0x200] at 0x100866b6.
    public static readonly short[] BarPersonQueue = new short[0x200];

    // *0x10080c40 (toc-0x7a20, ptr -> short): the mission-board SELECTED ROW —
    // the BBS list's selection (InitWeaponSlotList resets it, the BBS filters
    // cycle it) and the mission-info dialog's abort-button gate (-1 = disabled).
    public static short BbsSelectedRow;

    // Bar modal-filter proc key (was UPP source cell 0x10080c18 -> FUN_1000a3ac).
    public const int BarFilterProc = 0x1000a3ac;
}
