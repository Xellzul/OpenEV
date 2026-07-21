using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10060a1c (EV Override-11.c lines 40438-40539) — occasionally spawns the
// scripted "reinforcement" pers (slot 511, sentinel PersIndex KamikazePersIndex) behind the
// player; spawn chance and shield scale with InstallDays.
public static class SpawnReinforcement
{
    public static void Run()
    {
        var template = GameData.Pers[511];
        if (template.ShipType < 0)
        {
            return;
        }

        // Already a reinforcement in the field -> bail.
        for (short i = 1; i < ShipTable.Count; i++)
        {
            var existing = GameData.Ships[i];
            if (existing.IsActive != 0 && existing.PersIndex == ShipRecord.KamikazePersIndex)
            {
                return;
            }
        }

        // Base spawn chance (1-in-N) by play time.
        int chance;
        if (WorldState.InstallDays < 15)
        {
            chance = 10000;
        }
        else if (WorldState.InstallDays < 31)
        {
            chance = 700;
        }
        else if (WorldState.InstallDays < 61)
        {
            chance = 4000;
        }
        else
        {
            chance = 2000;
        }

        // High combat rating halves the chance denominator below, so reinforcements
        // actually spawn MORE often once the player is a proven killer, not less.
        if (WorldState.PlayerCombatRating > 399)
        {
            chance = (int)((double)(short)chance * MathConstants.Half);
        }

        if (SeedEvoRng.Run((short)(chance * 3)) != 0)
        {
            return;
        }

        short slot = (short)AllocateShipSlot.Run(GameData.Ships[0].CurrentSystem, 2);
        if (slot == -1)
        {
            return;
        }

        var ship = GameData.Ships[slot];
        ship.PersIndex = ShipRecord.KamikazePersIndex;
        ship.ShipClass = template.ShipType;
        ship.AiBehaviorType = ShipAiType.Interceptor;

        // Spread-sum a spawn distance, then place the ship behind the player.
        float epsilon = ShipStatConstants.NearestSearchEpsilon;
        float spreadAccum = epsilon;
        for (float spread = ShipStatConstants.ReinforceSpreadStart;
             spread > epsilon;
             spread -= ShipStatConstants.ReinforceSpreadStep)
        {
            spreadAccum += spread;
        }

        var player = GameData.Ships[0];
        ship.PosX = player.PosX;
        ship.PosY = player.PosY;
        EvMath.OffsetByHeading(
            (double)(float)-(ShipStatConstants.ReinforceBaseOffset + spreadAccum),
            player.Heading, ref ship.PosX, ref ship.PosY);

        ship.Heading = player.Heading;
        ship.DudeSpawnIndex = -1;
        ship.AiCourage = 4;
        ship.Govt = -1;

        var cls = GameData.ShipClasses[ship.ShipClass];
        for (int w = 0; w < ShipRecord.WeaponSlotCount; w++)
        {
            // Class default loadout plus the pers template's per-slot delta.
            ship.WeaponSlotType[w] = (short)(cls.DefaultWeaponType[w] + template.WeaponType[w]);
            ship.WeaponSlotAmmo[w] = (short)(cls.DefaultWeaponAmmo[w] + template.WeaponAmmo[w]);
            if (WorldState.InstallDays < 38)
            {
                ship.WeaponSlotAmmo[w] = 0;
            }
        }

        // cls.Shield is an int-valued cell (not a true float); the (float) cast below
        // is a plain numeric widen, matching SpawnPers.cs's identical shield setup.
        ship.Shield = (int)(template.ShieldMultiplier * (float)cls.Shield);
        ShipAi.ResetAiToIdle(ShipTable.Ships[slot]);
        ShipAi.SetStateWindDown(ShipTable.Ships[slot]);
        ship.LastVictimSlot = -1;
        if (WorldState.InstallDays > 60)
        {
            // ship.Shield holds an int-valued quantity here (see PickForwardWeaponForTarget /
            // TickShipAI for the same pattern) -- the (int) cast must stay, it is not a no-op.
            ship.Shield = (int)((double)(int)ship.Shield * ShipStatConstants.ReinforceShieldScale);
        }
    }
}
