using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1006b6b0 (EV Override-11.c lines 44206-44267). Spawns a player wingman/escort of
// class shipType: allocates a ship slot in the player's current system, copies the class's
// weapon loadout, and places it beside the player (originSpob == -1) or at a spob. Returns the
// slot, or -1 on failure.
public static class SpawnPlayerWingman
{
    public static int Run(short shipType, short originSpob)
    {
        int result = AllocateShipSlot.Run(Core.Model.GameData.Player.CurrentSystem, 2);
        short slot = (short)result;
        if (slot == -1 || shipType < 0)
        {
            return -1;
        }

        var ship = Core.Model.GameData.Ships[slot];
        var cls = Core.Model.GameData.ShipClasses[shipType];
        ship.ShipClass = shipType;
        ship.AiBehaviorType = ShipAiType.Escort;
        ship.OwnerSlot = 0;
        ship.Govt = -1;
        ship.AiCourage = 2;
        // Numeric assignment, not a raw bit-copy: the decompile does a 4-byte block transfer
        // (undefined4), but the shield cell holds a real int magnitude (real-save verified) —
        // don't "fix" this back to a bit-pattern reinterpret.
        ship.Shield = cls.Shield;
        ship.DudeSpawnIndex = -1;
        ship.HailQuoteSpoken = 0;
        ship.HasAfterburner = (byte)(HasAfterburner.Run(ShipTable.Ships[slot]) ? 1 : 0);
        ship.IsCarriedFighter = 1;
        ship.GrudgeMissionIndex = -1;
        ship.SpawningMissionSlot = -1;

        if (originSpob == -1)
        {
            ship.PosX = Core.Model.GameData.Player.PosX;
            ship.PosY = Core.Model.GameData.Player.PosY;
            ship.Heading = Core.Model.GameData.Player.Heading;
            // Nudge to a random heading at a short random distance from the player.
            double distance = (double)(float)((short)SeedEvoRng.Run(50) + 50);
            EvMath.OffsetByHeading(distance, (int)SeedEvoRng.Run(360),
                ref Core.Model.GameData.Ships[slot].PosX, ref Core.Model.GameData.Ships[slot].PosY);
        }
        else
        {
            ship.PosX = Core.Model.GameData.Spobs[originSpob].XPos;
            ship.PosY = Core.Model.GameData.Spobs[originSpob].YPos;
        }

        // Copy the class's default weapon loadout (type + ammo) into every weapon slot.
        for (int w = 0; w < ShipRecord.WeaponSlotCount; w++)
        {
            ship.WeaponSlotType[w] = cls.DefaultWeaponType[w];
            ship.WeaponSlotAmmo[w] = cls.DefaultWeaponAmmo[w];
        }

        ShipAi.ResetAiToIdle(ShipTable.Ships[slot]);
        ShipAi.SetStateHyperWindupAndPropagate(ShipTable.Ships[slot]);
        return result;
    }
}
