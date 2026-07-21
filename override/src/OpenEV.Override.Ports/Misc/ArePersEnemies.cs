using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_100084fc (EV Override-11.c lines 4546-4598).
public static class ArePersEnemies
{
    public static bool Run(int shipA, int shipB)
    {
        var aRec = ShipTable.FromPtr(shipA);
        var bRec = ShipTable.FromPtr(shipB);

        if (aRec.SlotIndex == bRec.SlotIndex)
        {
            return false;
        }
        if (aRec.IsActive == 0 || bRec.IsActive == 0)
        {
            return false;
        }
        if (bRec.PersIndex == ShipRecord.KamikazePersIndex)
        {
            return false;
        }
        if (aRec.Govt != -1 && bRec.Govt != -1)
        {
            if (aRec.Govt == bRec.Govt)
            {
                return false;
            }
            if (bRec.Govt == GameData.Governments[aRec.Govt].Enemy ||
                aRec.Govt == GameData.Governments[bRec.Govt].Enemy ||
                ((GameData.Governments[bRec.Govt].Flags & GovtFlags.Xenophobic) != 0 &&
                 bRec.Govt != GameData.Governments[aRec.Govt].Ally &&
                 aRec.Govt != GameData.Governments[bRec.Govt].Ally))
            {
                return true;
            }

            // decompile's local_a0[148]; only indices 0..127 are ever touched, but the
            // buffer is sized to match the original stack frame.
            var allyTable = new byte[148];
            for (short govtIndex = 0; govtIndex < GovtTable.Count; govtIndex = (short)(govtIndex + 1))
            {
                // NOTE (original-game quirk kept, OGB-47): this reset fires per-slot, inline
                // with the sweep that conditionally sets slots below — a hit written by an
                // earlier iteration at index < govtIndex gets wiped when the sweep reaches
                // that index. See ORIGINAL_GAME_BUGS.md.
                allyTable[govtIndex] = 0;
                if (aRec.Govt == GameData.Governments[govtIndex].Ally ||
                   govtIndex == GameData.Governments[aRec.Govt].Ally)
                {
                    for (short allyGovtIndex = 0; allyGovtIndex < GovtTable.Count; allyGovtIndex = (short)(allyGovtIndex + 1))
                    {
                        if (allyGovtIndex == GameData.Governments[govtIndex].Enemy)
                        {
                            allyTable[allyGovtIndex] = 1;
                        }
                    }
                }
            }
            if (allyTable[bRec.Govt] != 0)
            {
                return true;
            }
        }
        return false;
    }
}
