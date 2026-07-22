using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_1006ddf8 — builds the set of fleets eligible to spawn into the given
// system, per the fleet's LinkSyst rules and mission-control gate, then rolls
// one uniformly random slot in [0, FleetTable.Count) and spawns it only if
// that slot is eligible (a roll landing on an ineligible slot spawns nothing).
// Decompile: EV Override-11.c lines 44962-45049.
public static class SpawnRandomEligibleFleet
{
    public static void Run(int systemIndex)
    {
        byte[] eligible = new byte[FleetTable.Count];
        short systemIndexShort = (short)systemIndex;
        short eligibleCount = 0;

        for (short i = 0; i < FleetTable.Count; i++)
        {
            eligible[i] = 0;
            var fleet = GameData.Fleets[i];
            if (fleet.LeadShipType != -1)
            {
                var sys = SystTable.Store[systemIndexShort];
                if (IsLinkEligible(fleet.LinkSyst, systemIndexShort, sys)) eligible[i] = 1;
                if (IsBlockedByMissionGate(fleet)) eligible[i] = 0;
            }
            if (eligible[i] != 0) eligibleCount++;
        }

        if (eligibleCount > 0)
        {
            short chosen = (short)SeedEvoRng.Run(FleetTable.Count);
            if (eligible[chosen] != 0)
            {
                SpawnFleet.Run(systemIndex, chosen);
            }
        }
    }

    // FUN_1006ddf8 44977-45029 — true if the fleet's LinkSyst makes it eligible
    // to spawn into systemIndexShort: a direct system link, a sibling-system
    // alias (band 128..9999), or a govt id/ally/not-self/enemy relationship
    // reached through the calling system's government (bands 10000.., 15000..,
    // 20000.., 25000.. respectively).
    private static bool IsLinkEligible(short link, short systemIndexShort, SystRecord sys)
    {
        if (link == -1) return true;
        if (systemIndexShort == link) return true;
        if (link > 127 && link < 10000 && systemIndexShort == link - 128) return true;
        if (link > 9999 && link < 15000 && link - 10000 == sys.Govt) return true;
        // AtOrPastTable: plug-in flët data can push these band indexes past the 128-entry
        // govt table; the original reads heap garbage there without crashing (see GovtTable).
        if (link > 14999 && link < 20000 && sys.Govt > -1)
        {
            if (sys.Govt == GovtTable.AtOrPastTable(link - 15000).Ally) return true;
            if (link - 15000 == GameData.Governments[sys.Govt].Ally) return true;
        }
        if (link > 19999 && link < 25000 && sys.Govt > -1 && link - 20000 != sys.Govt) return true;
        if (link > 24999 && link < 30000 && sys.Govt > -1)
        {
            if (sys.Govt == GovtTable.AtOrPastTable(link - 25000).Enemy) return true;
            if (link - 25000 == GameData.Governments[sys.Govt].Enemy) return true;
        }
        return false;
    }

    // FUN_1006ddf8 45030-45039 — true when the fleet's MissionBit blocks it
    // from spawning: < 1000 must be SET, >= 1000 aliases bit (n-1000) which
    // must be CLEAR.
    private static bool IsBlockedByMissionGate(FleetRecord fleet)
    {
        if (fleet.MissionBit == -1) return false;
        if (fleet.MissionBit < 1000) return !ControlBits.IsSet(fleet.MissionBit);
        return ControlBits.IsSet(fleet.MissionBit - 1000);
    }
}
