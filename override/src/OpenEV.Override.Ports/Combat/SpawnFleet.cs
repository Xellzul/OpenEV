using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_1006e7bc — spawns a fleet (one leader ship + 0..N escorts per escort group)
// from the fleet defined at fleetIndex into systemIndex, if the fleet's mission
// control bit allows it. Positions the leader at a random spread/heading and
// scatters the escorts around it. Decompile: EV Override-11.c lines 45129-45271.
//
// The decompile reaches the spawn-physics floats through an uninitialized
// phantom local (local_7c); the port resolves them to the named ShipStatConstants.
public static class SpawnFleet
{
    public static void Run(int systemIndex, short fleetIndex)
    {
        var fleet = Core.Model.GameData.Fleets[fleetIndex];
        if (fleet.LeadShipType == -1) return;
        if (!PassesMissionControlGate(fleet)) return;

        short leaderSlot = SpawnLeader(systemIndex, fleet);
        if (leaderSlot == -1) return;

        SpawnEscorts(systemIndex, fleet, leaderSlot);
    }

    // FUN_1006e7bc 45150-45158 — ControlBits gate: -1 ungated; index < 1000 must be SET;
    // index >= 1000 aliases bit (index-1000), which must be CLEAR.
    private static bool PassesMissionControlGate(Systems.Model.FleetRecord fleet)
    {
        if (fleet.MissionBit == -1) return true;
        if (fleet.MissionBit < 1000) return Core.Model.ControlBits.IsSet(fleet.MissionBit);
        return !Core.Model.ControlBits.IsSet(fleet.MissionBit - 1000);
    }

    // FUN_1006e7bc 45160-45210 — allocate + configure the leader; returns -1 if no slot is free.
    private static short SpawnLeader(int systemIndex, Systems.Model.FleetRecord fleet)
    {
        short leaderSlot = (short)AllocateShipSlot.Run((short)systemIndex, 4);
        if (leaderSlot == -1) return -1;

        var leader = Core.Model.GameData.Ships[leaderSlot];
        var leaderShip = Ship.Model.ShipTable.Ships[leaderSlot];
        leader.ShipClass = fleet.LeadShipType;
        leader.Govt = fleet.Govt;

        var leaderClass = Core.Model.GameData.ShipClasses[leaderShip.ShipClass];
        leader.AiBehaviorType = leaderClass.InherentAI;
        // Class Shield (class+0x3a) is the INTEGER shield value (loader: ReadResourceShort, < 0 -> *-5),
        // so this numeric int->float copy recovers the live value. The ASM raw-copies the 32-bit word;
        // TickShipAI's matching morph copy is now aligned to this numeric form (was Int32BitsToSingle).
        leader.Shield = leaderClass.Shield;
        leader.PersIndex = -1;
        leader.GrudgeMissionIndex = -1;
        leader.HasAfterburner = (byte)(Ship.HasAfterburner.Run(leaderShip) ? 1 : 0);
        Ship.ShipAi.ResetAiToIdle(leaderShip);
        Ship.ShipAi.SetStateWindDown(leaderShip);

        float zero = Ship.Model.ShipStatConstants.SpawnZeroDefault;
        float spreadAccum = zero;
        for (float spread = Ship.Model.ShipStatConstants.SpawnSpreadStart;
             zero < spread;
             spread -= Ship.Model.ShipStatConstants.SpawnSpreadStep)
        {
            spreadAccum += spread;
        }
        leader.PosY = zero;
        leader.PosX = zero;
        EvoMath.EvMath.OffsetByHeading(
            (double)(Ship.Model.ShipStatConstants.SpawnBaseOffset + spreadAccum),
            (int)Misc.SeedEvoRng.Run(360), ref leader.PosX, ref leader.PosY);
        leader.Heading = (short)EvoMath.EvMath.HeadingBetween(leader.PosX, leader.PosY, zero, zero);

        leader.DockedSpobIndex = -2;
        leader.PriorSystem = -1;
        leader.VelY = zero;
        leader.VelX = zero;
        leader.JumpWindupTimer = -999;
        leader.AiTickStamp = (int)MacToolbox.TickCount();   // raw-int stamp (read back as raw int, not float bits)
        EvoMath.EvMath.OffsetByHeading(Ship.Model.ShipStatConstants.SpawnSpreadStart,
            leader.Heading, ref leader.VelX, ref leader.VelY);

        CopyDefaultWeapons(leader, leaderClass);
        return leaderSlot;
    }

    // FUN_1006e7bc 45211-45270 — roll each escort group's [min,max] count and spawn its members.
    private static void SpawnEscorts(int systemIndex, Systems.Model.FleetRecord fleet, short leaderSlot)
    {
        var leader = Core.Model.GameData.Ships[leaderSlot];
        for (int group = 0; group < Systems.Model.FleetRecord.EscortGroupCount; group++)
        {
            int range = fleet.EscortMax[group] - fleet.EscortMin[group] + 1;
            short count = (short)(fleet.EscortMin[group] + (short)Misc.SeedEvoRng.Run((short)range));
            for (short i = 0; i < count; i++)
            {
                short memberSlot = (short)AllocateShipSlot.Run((short)systemIndex, 2);
                if (memberSlot == -1) continue;
                SpawnEscortMember(memberSlot, leaderSlot, leader, fleet, group);
            }
        }
    }

    // FUN_1006e7bc 45217-45266 — configure one escort scattered around the leader (shares its heading/velocity).
    private static void SpawnEscortMember(short memberSlot, short leaderSlot,
        Ship.Model.ShipRecord leader, Systems.Model.FleetRecord fleet, int group)
    {
        var member = Core.Model.GameData.Ships[memberSlot];
        var memberShip = Ship.Model.ShipTable.Ships[memberSlot];
        member.AiBehaviorType = ShipAiType.Escort;
        member.OwnerSlot = leaderSlot;
        member.ShipClass = fleet.EscortType[group];
        member.DudeSpawnIndex = -1;
        member.PersIndex = -1;
        member.Govt = fleet.Govt;

        var memberClass = Core.Model.GameData.ShipClasses[memberShip.ShipClass];
        member.Shield = memberClass.Shield;   // numeric int->float copy (see SpawnLeader)
        Ship.ShipAi.ResetAiToIdle(memberShip);
        CopyDefaultWeapons(member, memberClass);

        member.Heading = leader.Heading;
        member.PosX = Ship.Model.ShipStatConstants.CaptureOffsetN150 + leader.PosX + (float)(short)Misc.SeedEvoRng.Run(300);
        member.PosY = Ship.Model.ShipStatConstants.CaptureOffsetN150 + leader.PosY + (float)(short)Misc.SeedEvoRng.Run(300);
        member.VelX = leader.VelX;
        member.VelY = leader.VelY;
        member.DockedSpobIndex = -2;
        member.PriorSystem = -2;
        member.JumpWindupTimer = -999;
        member.AiTickStamp = (int)MacToolbox.TickCount();
        Ship.ShipAi.SetStateWindDown(memberShip);
    }

    // FUN_1006e7bc 45201-45209 / 45231-45240 — copy the class's default weapon loadout (leader + escorts).
    private static void CopyDefaultWeapons(Ship.Model.ShipRecord ship, Ship.Model.ShipClassRecord shipClass)
    {
        for (int w = 0; w < Ship.Model.ShipRecord.WeaponSlotCount; w++)
        {
            ship.WeaponSlotType[w] = shipClass.DefaultWeaponType[w];
            ship.WeaponSlotAmmo[w] = shipClass.DefaultWeaponAmmo[w];
        }
    }
}
