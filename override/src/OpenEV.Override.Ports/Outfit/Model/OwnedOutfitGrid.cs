namespace OpenEV.Override.Ports.Outfit.Model;

// The player's owned-outfit count grid: originally a fixed BSS short[128] at
// 0x100900fa, indexed by outfit index (0..127), value = how many the player owns.
// Now a managed short[] array.
public static class OwnedOutfitGrid
{
    public const int Base = 0x100900fa;
    public const int Stride = 2;
    public const int Count = 128;

    public static readonly short[] Store = new short[Count];
}
