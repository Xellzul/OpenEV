namespace OpenEV.Override.Ports.Outfit.Model;

// The "special weapon-slot -> outfit-index" map: the two extra fire-mode tabs
// (fire-mode slots 6 and 7) each hold a 'jünk' special-weapon outfit index, or
// -1 (none). ShowCommodityExchangeDialog fills it; HasWeaponInSlot /
// DrawCommodityTradeDialog read it.
//
// Originally a 2-entry short[] reached through the PEF-relocated pointer cell
// PTR_DAT_1008105c. Now a managed short[2]. The BSS-zero C# default is matched only for
// completeness — every real reader is reachable solely from within
// ShowCommodityExchangeDialog, which unconditionally writes both slots to -1
// at its own entry before any read.
public static class WeaponSlotOutfitMap
{
    public const int Count = 2;
    public static readonly short[] Store = new short[Count];
}
