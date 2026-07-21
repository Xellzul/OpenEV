namespace OpenEV.Override.Ports.Outfit.Model;

// Managed home for the shipyard / outfitter shop UI state that used to live in
// raw data-segment globals.
public static class ShipyardState
{
    // 0x1008f87a: the shipyard's "available row" index map. For each visible
    // shop row it holds the
    // ship-class (or outfit) index that row shows, or -1 (the decompile's 0xffff)
    // for an empty slot. Was a fixed BSS short[128]; now a managed array.
    //
    public const int Count = 128;
    public static readonly short[] AvailableRowIndex = new short[Count];

    // RenderShipyardButtonRow/TrackShipyardButtonHit only ever scan the first 64 of
    // the 128 slots above (matches their own ASM/decompile, FUN_1000c56c/FUN_1000c244,
    // both `< 0x40`) — a narrower range than Count, not a bug; BuildAvailableShipList
    // likewise only ever fills indices [0, 64).
    public const int ButtonRowScanLimit = 64;

    // 0x10080be4 (PTR_DAT_10080be4): was a ptr cell -> short escort-shipyard mode
    // flag. 0 = buy-ship shipyard, nonzero = buy-escort mode (which hides every
    // mission-gated class). Managed field now.
    public static short EscortMode;

    // ── Shipyard dialog (FUN_1003a500, DLOG 1004) globals — old ptr cells ──
    // *0x10080ca8 ("OutfitDialogPtrSlot" misname): the open shipyard dialog window.
    public static int DialogWindow;
    // *0x10080ca0 ("TradeListSelectedRowPtrSlot"): selected grid row (-1 = none).
    public static short SelectedRow;
    // *0x10080c94 ("CommodityScrollCtlPtrSlot" misname): the 12-entry shipyard
    // PICT-handle array ([0..3] buttons 0x1b5e.. or escort 0x1ba0.., [4..7]
    // 0x1b62/63/0x1bcc/cd pairs, [8..11] 0x1b74..0x1b77).
    public static readonly int[] Picts = new int[12];
    // *0x10080ff8: selected grid slot (-1 = none; the filter's hit cell).
    public static short SelectedSlot;
    // *0x10081014: secondary selected-slot cell (-1 = none).
    public static short SelectedSlotB;
    // *0x10080c9c ("TradeListFirstRowPtrSlot"): first visible grid row (page scroll ±4).
    public static short FirstVisibleRow;
    // *0x10080ca4 ("TradeBuyEnableFlagPtrSlot"): buy/confirm enabled byte.
    public static byte BuyEnabled;
    // *0x10081018 ("OutfitListCtlPtrSlot" misname): the selected ship's PICT
    // Handle (class+5000; the cheat ship is PICT 5200).
    public static int SelectedShipPict;
    // *0x10081010 (toc-0x7650): the previous grid-click event time (double-click
    // detect: same slot within 16 ticks opens the specs dialog).
    public static int LastClickWhen;
    // Modal-filter proc key (was UPP source cell 0x10080ffc -> FUN_1003b444).
    public const int FilterProc = 0x1003b444;

    // *(*0x10080fec) ("MissionsSubDialogRecordSlot" misname): the ship-specs
    // sub-dialog (DLOG 0x3ed) window RunShipSpecsDialog opens over the shipyard;
    // DrawShipyardInfoDialog / PictureDialogFilter redraw through it.
    public static int SpecsDialogWindow;
    // Specs-dialog modal-filter proc key (the PEF-relocated UPP source cell
    // 0x10080ff0 holds FUN_1003c864 = Dialog.PictureDialogFilter).
    public const int SpecsFilterProc = 0x1003c864;
}
