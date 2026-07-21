namespace OpenEV.Override.Ports.Outfit.Model;

// Semantic accessor for the outfit/weapon definition table (the 'oütf' resource
// records). 0x1008a518 (`_DAT_1008a518`) holds a POINTER to record[0]; each record
// is 0x58 bytes, indexed by outfit index (0..127, 128 records).
// Heap-pointer-table contract (see ShipTable/SystTable): the deref is baked into
// Base so a bare `0x1008a518 + …` can't drop it.
public static class OutfitTable
{
    public const int PtrSlot = 0x1008a518;
    public const int Stride = 0x58;
    public const int Count = 128;

    // Synthetic record-base in the 0x30 FREE band — index arithmetic only, records
    // live in Store[]. NOT 0x60 (= MacPixMap.HandleBase).
    public const int Base = 0x3060_0000;

    // Typed managed backing for the 128 outfit records; OutfitRec maps its Ptr to Store[index].
    public static readonly OutfitRecord[] Store = CreateStore();
    private static OutfitRecord[] CreateStore()
    {
        var s = new OutfitRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new OutfitRecord();
        return s;
    }

    // Indexable + foreach view over the records: `OutfitTable.Outfits[i].ModType[bank]`.
    public static readonly OutfitArray Outfits = default;

    // True if the player owns any outfit whose ModType (either bank) matches. Shared by
    // the Has* outfit predicates (HasDensityScanner, HasIffRadar, HasCloakingDevice, …).
    public static bool PlayerHasOutfit(OutfitModType modType)
    {
        for (int slot = 0; slot < Count; slot++)
        {
            if (OwnedOutfitGrid.Store[slot] <= 0)
                continue;

            var outfit = Outfits[slot];
            if (outfit.ModType[0] == modType || outfit.ModType[1] == modType)
                return true;
        }
        return false;
    }

    // Sum of ModValue * OwnedCount across all outfit slots for the given ModType.
    // Shared by ShipDerivedStats.Effective{Armor,Fuel,Shield,Cargo}Max /
    // EffectiveHyperRangeSquared / InterferenceReduction / EffectiveManeuver.
    // guardOwnedPositive: Fuel/Shield/HyperRange skip owned<=0 slots (the true default);
    // Armor/Cargo/InterferenceReduction/Maneuver add ModValue*owned unconditionally
    // (pass false — a negative owned count then subtracts).
    public static int SumOutfitModValue(OutfitModType modType, bool guardOwnedPositive = true)
    {
        int total = 0;
        for (int slot = 0; slot < Count; slot++)
        {
            short owned = OwnedOutfitGrid.Store[slot];
            if (guardOwnedPositive && owned <= 0)
                continue;

            var outfit = Outfits[slot];
            for (int bank = 0; bank < OutfitRecord.ModBankCount; bank++)
            {
                if (outfit.ModType[bank] == modType)
                    total += outfit.ModValue[bank] * owned;
            }
        }
        return total;
    }
}

// Stateless indexer holder exposed as `OutfitTable.Outfits`: an indexable +
// foreach-able view over the 128 outfit records. Each yielded OutfitRec carries
// its own .Index.
public readonly struct OutfitArray
{
    public OutfitRec this[int index] => new OutfitRec(OutfitTable.Base + index * OutfitTable.Stride);

    public Enumerator GetEnumerator() => new Enumerator();

    public struct Enumerator
    {
        private int _i = -1;
        public Enumerator() { }
        public bool MoveNext() => ++_i < OutfitTable.Count;
        public OutfitRec Current => new OutfitRec(OutfitTable.Base + _i * OutfitTable.Stride);
    }
}
