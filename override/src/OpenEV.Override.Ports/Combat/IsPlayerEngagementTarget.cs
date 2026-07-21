using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10007ec8 (EV Override-11.c lines 4389-4499).
// True when this NPC may treat the player as an engagement target — its active grudge
// mission allows it, or its govt's relation to the player's current-system govt and
// per-system legal standing does.
public static class IsPlayerEngagementTarget
{
    public static bool Run(ShipRec ship)
    {
        if (ShipAi.HasEngagedAllyOrCarrier(ship))
        {
            return false;
        }
        if (ship.OwnerSlot == 0)
        {
            return true;
        }
        if (ship.GrudgeMissionIndex != -1)
        {
            return GrudgeTargetsPlayer(ship);
        }
        return LegalStatusTargetsPlayer(ship);
    }

    // FUN_10007ec8 4477-4491 — the NPC's active grudge mission decides: only the
    // Escort/Observe goals with a protect-player ShipBehav (units digit 1, i.e. 1 or 11)
    // target the player.
    private static bool GrudgeTargetsPlayer(ShipRec ship)
    {
        if (Core.Model.GameData.MissionStates[ship.GrudgeMissionIndex].IsActive == 0)
        {
            return false;
        }
        var grudge = Core.Model.GameData.Missions[ship.GrudgeMissionIndex];
        if (grudge.MissionGoalType != MissionGoalKind.Escort && grudge.MissionGoalType != MissionGoalKind.Observe)
        {
            return false;
        }
        if (grudge.ShipBehavior != -1)
        {
            if (grudge.ShipBehavior == 1 || grudge.ShipBehavior == 11)
            {
                return true;
            }
            if (grudge.ShipBehavior == 0 || grudge.ShipBehavior == 10)
            {
                return false;
            }
        }
        return false;
    }

    // FUN_10007ec8 4405-4476 — no grudge: decide from the NPC's govt vs the player's
    // current-system govt and per-system legal standing.
    private static bool LegalStatusTargetsPlayer(ShipRec ship)
    {
        if (ship.Govt == -1)
        {
            return true;
        }

        short playerSyst = Core.Model.GameData.Player.CurrentSystem;
        short systGovt = SystTable.Store[playerSyst].Govt;
        short legal = GalaxyMapGlobals.SystemStatus(playerSyst);
        var govtFlags = Core.Model.GameData.Governments[ship.Govt].Flags;

        // Xenophobic govts pre-filter on the player's standing in the current system; if
        // none of these cases decides, fall through to the govt-relation check below.
        if ((govtFlags & GovtFlags.Xenophobic) != 0)
        {
            if (ship.Govt == systGovt)
            {
                if (Core.Model.GameData.Governments[systGovt].CrimeTolerance < legal)
                {
                    return true;
                }
            }
            else if (systGovt < 0)
            {
                // Faithful original quirk: reads govt record 0's CrimeTolerance (+0x26),
                // not the system govt's.
                if (Core.Model.GameData.Governments[0].CrimeTolerance < legal)
                {
                    return false;
                }
            }
            else if (Core.Model.GameData.Governments[systGovt].CrimeTolerance < legal)
            {
                return false;
            }
        }

        // Govt-relation decision. Law-enforcement govts always target; others only when
        // the player's legal standing exceeds the relevant govt's crime tolerance.
        if (systGovt < 0)
        {
            return (govtFlags & GovtFlags.LawEnforcementEverywhere) == 0
                || LegalWithinTolerance(ship.Govt, legal);
        }
        if (GovtsAllied(ship.Govt, systGovt))
        {
            return LegalWithinTolerance(systGovt, legal);
        }
        if (GovtsAtWar(ship.Govt, systGovt))
        {
            return !LegalWithinTolerance(systGovt, legal);
        }
        return (govtFlags & GovtFlags.LawEnforcementEverywhere) == 0
            || LegalWithinTolerance(systGovt, legal);
    }

    // -CrimeTolerance(govt) <= legal (+0x26) — the govt tolerates the player's standing.
    private static bool LegalWithinTolerance(short govtIndex, short legal)
        => -Core.Model.GameData.Governments[govtIndex].CrimeTolerance <= legal;

    // Allied if either govt lists the other as its Ally (+0x22).
    private static bool GovtsAllied(short a, short b)
        => a == Core.Model.GameData.Governments[b].Ally || Core.Model.GameData.Governments[a].Ally == b;

    // At war if either govt lists the other as its Enemy (+0x24).
    private static bool GovtsAtWar(short a, short b)
        => a == Core.Model.GameData.Governments[b].Enemy || Core.Model.GameData.Governments[a].Enemy == b;
}
