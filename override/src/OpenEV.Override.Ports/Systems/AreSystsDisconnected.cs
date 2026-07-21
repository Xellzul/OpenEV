using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
namespace OpenEV.Override.Ports.Systems;

// Port of FUN_1005e398 from EV Override-11.c lines 39136-39172. Two spobs are "disconnected" when their systems are
// not equal, not both shown, and not directly hyperlinked either way. Re-derived
// onto the typed managed SystTable.Store.
public static class AreSystsDisconnected
{
    public static bool Run(int spobA, int spobB)
    {
        // DEVIATION (faithful): the self-reference spob can be -1 (no active nav target —
        // ResolveSingleMissionSpawn's anchorSpob = Ships[0].NavTargetSpob when DrawGateFlag==0).
        // The original derefed a pointer 0x48 bytes before the spob heap (its +0x04 read
        // lands 0x44 bytes before it) as a garbage system id; the typed table can't
        // reproduce that OOB read (and the garbage id could itself index outside
        // SystTable.Store), so out-of-range spobs are reported as disconnected here — the
        // closest faithful approximation of this function's own result.
        if ((uint)spobA >= (uint)SpobTable.Count || (uint)spobB >= (uint)SpobTable.Count) return true;
        short systA = GameData.Spobs[spobA].System;
        short systB = GameData.Spobs[spobB].System;

        if (systA == systB)
        {
            return false;
        }
        var systRecA = SystTable.Store[systA];
        var systRecB = SystTable.Store[systB];
        if (systRecA.ShownFlag == 0)
        {
            return true;
        }
        if (systRecB.ShownFlag == 0)
        {
            return true;
        }
        for (short linkIdx = 0; linkIdx < SystRecord.HyperLinkCount; linkIdx = (short)(linkIdx + 1))
        {
            if (systRecA.HyperLink[linkIdx] != -1 && systB == systRecA.HyperLink[linkIdx])
            {
                return false;
            }
            if (systRecB.HyperLink[linkIdx] != -1 && systA == systRecB.HyperLink[linkIdx])
            {
                return false;
            }
        }
        return true;
    }
}
