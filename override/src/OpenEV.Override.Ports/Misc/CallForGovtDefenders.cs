// Port of FUN_1000870c (EV Override-11.c lines 4599-4698).

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Misc;

public static class CallForGovtDefenders
{
    public static void Run(int attacker, int target)
    {
        var aRec = ShipTable.FromPtr(attacker);
        var tRec = ShipTable.FromPtr(target);
        if (aRec.PersIndex != ShipRecord.KamikazePersIndex && tRec.PersIndex != ShipRecord.EngagePlayerPersIndex &&
           (aRec.Govt == -1 ||
            (GameData.Governments[aRec.Govt].Flags & GovtFlags.StartDisabledOrDerelict) == 0))
        {
            for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex = (short)(shipIndex + 1))
            {
                var sRec = ShipTable.Ships[shipIndex];
                if ((sRec.AiBehaviorType == ShipAiType.Warship || sRec.AiBehaviorType == ShipAiType.Interceptor) &&
                   sRec.AiState != ShipAiState.AttackShip)
                {
                    var shouldDefend = sRec.PersIndex != ShipRecord.KamikazePersIndex;
                    if (ArePersEnemies.Run(sRec.Ptr, attacker))
                    {
                        return;
                    }
                    if (ShouldGovtDefenderEngage(shouldDefend, sRec, aRec, tRec))
                    {
                        sRec.AiState = ShipAiState.AttackShip;
                        sRec.TargetSlot = tRec.SlotIndex;
                    }
                }
            }
        }
    }

    // FUN_1000870c 4619-4686 — the govt/ally/legal-status veto chain: given the seed decision
    // (candidate isn't the Kamikaze pers), walk enemy/ally relations between the candidate defender
    // (sRec), the attacker (aRec), and the target (tRec), then apply the Xenophobic /
    // LawEnforcementEverywhere / StartDisabledOrDerelict / IgnoreInGoodSamaritan flag vetoes.
    private static bool ShouldGovtDefenderEngage(bool shouldDefend, ShipRec sRec, ShipRec aRec, ShipRec tRec)
    {
        if (sRec.Govt != -1 && aRec.Govt != -1)
        {
            shouldDefend = GameData.Governments[aRec.Govt].Enemy != sRec.Govt &&
                    aRec.Govt != GameData.Governments[sRec.Govt].Enemy &&
                    shouldDefend;
            if ((GameData.Governments[aRec.Govt].Flags & GovtFlags.Xenophobic) != 0 &&
               aRec.Govt != sRec.Govt)
            {
                shouldDefend = false;
            }
            var isAlly = false;
            if (aRec.Govt == GameData.Governments[sRec.Govt].Ally &&
               GameData.Governments[sRec.Govt].Ally != -1)
            {
                isAlly = true;
            }
            if (GameData.Governments[aRec.Govt].Ally == sRec.Govt &&
               GameData.Governments[aRec.Govt].Ally != -1)
            {
                isAlly = true;
            }
            if (aRec.Govt == sRec.Govt)
            {
                isAlly = true;
            }
            if ((GameData.Governments[sRec.Govt].Flags & GovtFlags.LawEnforcementEverywhere) == 0 && !isAlly)
            {
                shouldDefend = false;
            }
            if (tRec.Govt == GameData.Governments[sRec.Govt].Ally &&
               GameData.Governments[sRec.Govt].Ally != -1)
            {
                shouldDefend = false;
            }
            if (tRec.Govt != -1 &&
               GameData.Governments[tRec.Govt].Ally == sRec.Govt &&
               GameData.Governments[tRec.Govt].Ally != -1)
            {
                shouldDefend = false;
            }
        }
        if (sRec.Govt != -1 && aRec.Govt == -1 &&
           (GameData.Governments[sRec.Govt].Flags & GovtFlags.LawEnforcementEverywhere) == 0)
        {
            shouldDefend = false;
        }
        if (aRec.Govt != -1)
        {
            if ((GameData.Governments[aRec.Govt].Flags & GovtFlags.StartDisabledOrDerelict) != 0)
            {
                shouldDefend = false;
            }
            if ((GameData.Governments[aRec.Govt].Flags & GovtFlags.IgnoreInGoodSamaritan) != 0)
            {
                shouldDefend = false;
            }
        }
        if (sRec.Govt == -1 && aRec.Govt != -1 &&
           (GameData.Governments[aRec.Govt].Flags & GovtFlags.Xenophobic) != 0)
        {
            shouldDefend = false;
        }
        return shouldDefend;
    }
}
