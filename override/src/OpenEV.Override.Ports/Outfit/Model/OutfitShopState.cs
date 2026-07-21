namespace OpenEV.Override.Ports.Outfit.Model;

// Managed home for the OUTFITTER dialog globals (Misc.AdvanceLoadout =
// FUN_10037ee4, DLOG 1002, + OutfitShopFilter/DrawOutfitShop/button rows).
public static class OutfitShopState
{
    // The available-row display-index map (was the DIRECT BSS short[128] at
    // 0x1008f77a): grid row -> outfit index, -1 sentinel. Written by
    // BuildAvailableOutfitList, read by the outfitter dialog family.
    public const int RowCount = 128;
    public static readonly short[] AvailableRowIndex = new short[RowCount];

    // *0x10080c90 ("TradeDialogCtlPtrSlot" misname): the open outfitter window.
    public static int DialogWindow;

    // *0x10081030: selected outfit row (-1 = none).
    public static short SelectedRow;

    // *0x1008102c: selected grid slot (-1 = none).
    public static short SelectedSlot;

    // *0x10080c88 ("CommodityCountCtlPtrSlot" misname): first visible grid row
    // (page scroll ±4).
    public static short FirstVisibleRow;

    // *0x10080c8c ("TradeDialogFlagPtrSlot" misname): sell-enabled byte.
    public static byte SellEnabled;

    // *0x10081028: a Map outfit (OutfitModType.Map = 16) was bought this visit.
    public static byte MapOutfitBought;

    // *0x10081024: a StatusClear outfit (OutfitModType.StatusClear = 21) was bought this visit.
    public static byte StatusClearBought;

    // *0x10080c80 ("CommodityDialogScratchPtrSlot" misname): the outfitter's
    // 10-entry icon-strip PICT array (7000..7005 + 7028..7031).
    public static readonly int[] Picts = new int[10];

    // *0x1008101c ("OutfitListCtlPtr2Slot" misname): the filter's persistent 2-byte
    // ×N-multiplier keymap state. Each poll: a local copy is taken, the buffer is
    // zeroed, then re-set from the LIVE key test (decompile keys 0x32/0x3f — see
    // OutfitShopFilter's PORT DEVIATION note) — the local copy is only used to
    // detect a change since the previous poll (invalidates dialog item 3, the
    // ×N readout) and is otherwise discarded, never "restored."
    public static readonly byte[] KeyFlagsSnapshot = new byte[2];

    // Modal-filter proc key (was UPP cell 0x10081034, the "AiRoutineProcSlot"
    // misname -> FUN_1003904c).
    public const int FilterProc = 0x1003904c;
}
