using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004d8b8 (EV Override-11.c 31893-32132).
//
// True when bar-person/mission `availSlot` may appear at the current spaceport:
// every flags[] criterion (location, control-bit gates, system status, player
// score, odds roll, cargo space, ship class, AI tier) must pass, and no active
// govt may have claimed the slot.
public static class IsBarPersEligible
{
    public static bool Run(short availSlot)
    {
        var rec = GameData.MissionAvail[availSlot];
        var playerShip = GameData.Player;
        byte[] flags = new byte[9];

        if (rec.LocationSelector < -31999)
        {
            return false;
        }
        if (rec.AppearOdds < 1)
        {
            return false;
        }
        if (rec.AvailLocation == -1)
        {
            return false;
        }
        if (rec.AvailLocation == 2)
        {
            if (RenderGlobals.DrawGateFlag == 0)
            {
                return false;
            }
        }
        else
        {
            if (RenderGlobals.DrawGateFlag != 0)
            {
                return false;
            }
            if (SpaceportGlobals.InBarFlag != 0 && rec.AvailLocation == 0)
            {
                return false;
            }
            if (SpaceportGlobals.InBarFlag == 0 && rec.AvailLocation == 1)
            {
                return false;
            }
        }

        // flags[0] — LOCATION: LocationSelector selects by banded value (spob id /
        // system link / govt relations).
        if (rec.LocationSelector == -1 || RenderGlobals.DrawGateFlag != 0)
        {
            flags[0] = 1;
        }
        else if (rec.LocationSelector < 128 || 4999 < rec.LocationSelector)
        {
            if (rec.LocationSelector < 5000 || 9998 < rec.LocationSelector)
            {
                if (rec.LocationSelector < 9999 || 14999 < rec.LocationSelector)
                {
                    if (rec.LocationSelector < 15000 || 19999 < rec.LocationSelector)
                    {
                        if (rec.LocationSelector < 20000 || 29999 < rec.LocationSelector)
                        {
                            short curSpobGovt = CurrentSpob.Rec.Govt;
                            // Provably dead: the outer band gate plus `24999 < LocSel` only admits
                            // LocSel > 29999, which `LocSel < 30000` then excludes. Keep the < 30000
                            // test — without it LocSel >= 30000 would index
                            // GameData.Governments[LocSel-25000] (>= 5000) out of range.
                            if (24999 < rec.LocationSelector && rec.LocationSelector < 30000 && curSpobGovt != -1)
                            {
                                if (curSpobGovt == GameData.Governments[rec.LocationSelector - 25000].Enemy)
                                {
                                    flags[0] = 1;
                                }
                                if (GameData.Governments[curSpobGovt].Enemy == rec.LocationSelector - 25000)
                                {
                                    flags[0] = 1;
                                }
                            }
                        }
                        else if (CurrentSpob.Rec.Govt != rec.LocationSelector - 20000)
                        {
                            flags[0] = 1;
                        }
                    }
                    else
                    {
                        short curSpobGovt = CurrentSpob.Rec.Govt;
                        if (curSpobGovt != -1)
                        {
                            if (curSpobGovt == GameData.Governments[rec.LocationSelector - 15000].Ally)
                            {
                                flags[0] = 1;
                            }
                            if (GameData.Governments[curSpobGovt].Ally == rec.LocationSelector - 15000)
                            {
                                flags[0] = 1;
                            }
                        }
                    }
                }
                else if (CurrentSpob.Rec.Govt == rec.LocationSelector - 10000)
                {
                    flags[0] = 1;
                }
            }
            else
            {
                foreach (short link in SystTable.Store[playerShip.CurrentSystem].HyperLink)
                {
                    if (link == rec.LocationSelector - 5000)
                    {
                        flags[0] = 1;
                        break;
                    }
                }
            }
        }
        else if (playerShip.NavTargetSpob == rec.LocationSelector - 128)
        {
            flags[0] = 1;
        }

        // flags[1] — control-bit gate (1000..1511 = alias must be CLEAR).
        if (rec.RequireBit == -1)
        {
            flags[1] = 1;
        }
        else if (rec.RequireBit < 0 || 511 < rec.RequireBit)
        {
            if (999 < rec.RequireBit && rec.RequireBit < 1512 && ControlBits.Get(rec.RequireBit - 1000) == 0)
            {
                flags[1] = 1;
            }
        }
        else if (ControlBits.Get(rec.RequireBit) != 0)
        {
            flags[1] = 1;
        }

        // flags[6] — control-bit NOT-gate.
        if (rec.ForbidBit == -1)
        {
            flags[6] = 1;
        }
        else if (-1 < rec.ForbidBit && rec.ForbidBit < 512 && ControlBits.Get(rec.ForbidBit) == 0)
        {
            flags[6] = 1;
        }

        // flags[2] — system-status / trading requirement.
        if (rec.RecordRequirement == 0)
        {
            flags[2] = 1;
        }
        else if (rec.RecordRequirement < -31999 && RenderGlobals.DrawGateFlag == 0)
        {
            if (rec.RecordRequirement == -32000 && GameData.Spobs[playerShip.NavTargetSpob].TradingEnabled != 0)
            {
                flags[2] = 1;
            }
            else if (rec.RecordRequirement == -32001)
            {
                foreach (var spob in GameData.Spobs)
                {
                    if (spob.Visible != 0 && spob.TradingEnabled != 0)
                    {
                        flags[2] = 1;
                        break;
                    }
                }
            }
        }
        else if (rec.RecordRequirement < 1)
        {
            if (GalaxyMapGlobals.SystemStatus(playerShip.CurrentSystem) <= rec.RecordRequirement)
            {
                flags[2] = 1;
            }
        }
        else if (rec.RecordRequirement <= GalaxyMapGlobals.SystemStatus(playerShip.CurrentSystem))
        {
            flags[2] = 1;
        }

        // flags[3] — player-score requirement.
        if (rec.ScoreRequirement < 1)
        {
            flags[3] = 1;
        }
        else if (rec.ScoreRequirement <= WorldState.PlayerCombatRating)
        {
            flags[3] = 1;
        }

        // flags[4] — appearance odds roll.
        if (rec.AppearOdds < 100)
        {
            if (GameData.RandomOdds[availSlot] <= rec.AppearOdds)
            {
                flags[4] = 1;
            }
        }
        else
        {
            flags[4] = 1;
        }

        // flags[5] — cargo-space requirement (gate persons only).
        if (RenderGlobals.DrawGateFlag == 0)
        {
            flags[5] = 1;
        }
        else if (rec.CargoSpaceRequired < 1)
        {
            flags[5] = 1;
        }
        else
        {
            short freeSpace = (short)FreeCargoSpaceWithMissions.Run();
            flags[5] = (byte)(freeSpace < rec.CargoSpaceRequired ? 0 : 1);
        }

        // A nonzero 'ëbug' availability-override byte forces the odds roll to pass.
        if (MissionAvailTable.AvailOverride != 0)
        {
            flags[4] = 1;
        }

        // flags[7] — ship-class / govt selector (banded).
        if (rec.AvailShipType < 128 || 192 < rec.AvailShipType)
        {
            if (rec.AvailShipType < 1128 || 1192 < rec.AvailShipType)
            {
                if (rec.AvailShipType < 2128 || 2256 < rec.AvailShipType)
                {
                    if (rec.AvailShipType < 3128 || 3256 < rec.AvailShipType)
                    {
                        flags[7] = 1;
                    }
                    else if (GameData.ShipClasses[playerShip.ShipClass].InherentGovt != rec.AvailShipType - 3128)
                    {
                        flags[7] = 1;
                    }
                }
                else if (GameData.ShipClasses[playerShip.ShipClass].InherentGovt == rec.AvailShipType - 2128)
                {
                    flags[7] = 1;
                }
            }
            else if (playerShip.ShipClass != rec.AvailShipType - 1128)
            {
                flags[7] = 1;
            }
        }
        else if (playerShip.ShipClass == rec.AvailShipType - 128)
        {
            flags[7] = 1;
        }

        // flags[8] — AI-tier gate. MisnFlags.BanFreighterPlayer disqualifies every
        // tier: the two conditions cover InherentAI < 3 and > 2, so the flag always
        // clears flags[8] (kept bug).
        flags[8] = 1;
        if (((MisnFlags)rec.Flags & MisnFlags.BanFreighterPlayer) != 0 &&
            GameData.ShipClasses[playerShip.ShipClass].InherentAI < ShipAiType.Warship)
        {
            flags[8] = 0;
        }
        if (((MisnFlags)rec.Flags & MisnFlags.BanFreighterPlayer) != 0 &&
            ShipAiType.BraveTrader < GameData.ShipClasses[playerShip.ShipClass].InherentAI)
        {
            flags[8] = 0;
        }

        foreach (byte flag in flags)
        {
            if (flag == 0)
            {
                return false;
            }
        }
        // An active govt that owns this mission-definition slot blocks it.
        for (short missionSlot = 0; missionSlot < MissionStateTable.Count; missionSlot++)
        {
            if (GameData.MissionStates[missionSlot].IsActive != 0 &&
                availSlot == GameData.Missions[missionSlot].MissionDefIndex)
            {
                return false;
            }
        }
        return true;
    }
}
