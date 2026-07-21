namespace OpenEV.Override.Ports.Outfit.Model;

// Managed home for the COMMODITY TRADE dialog globals (Outfit.
// ShowCommodityExchangeDialog = FUN_10034c20 + CommodityExchangeFilter/
// DrawCommodityTradeDialog/DrawTradeButtonRow/TrackTradeButtonRow/
// CycleTradeTab).
public static class TradeGlobals
{
    // *0x10080c70 ("CommodityDialogPtrSlot"): the open trade dialog window.
    public static int DialogWindow;

    // *0x10080c68: the trade dialog's 6-entry PICT-handle array.
    public static readonly int[] Picts = new int[6];

    // Modal-filter proc key (was UPP cell 0x1008106c -> FUN_1003579c).
    public const int FilterProc = 0x1003579c;
}
