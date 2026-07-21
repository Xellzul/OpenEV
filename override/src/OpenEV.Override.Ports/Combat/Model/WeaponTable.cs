using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat.Model;

// The in-memory "wëap" weapon-definition table. 0x1008a510 (`_DAT_1008a510`) held a
// POINTER to record[0], 64 records of 0x28 bytes (alloc 0xa00 at toc+0x1eb0). The
// records now live in the typed managed Store. Populated by LoadSpobAndStellarResources
// (writes Store directly). Indexed by weapon index, or by a ship's current-weapon slot
// (ship+0x32).
public static class WeaponTable
{
    public const int Count = 64;

    public static readonly WeaponRecord[] Store = CreateStore();
    private static WeaponRecord[] CreateStore()
    {
        var s = new WeaponRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new WeaponRecord();
        return s;
    }
}
