using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10067ba4 (EV Override-11.c lines 43120-43237) — launches a guidance-type-99
// "special weapon" that is itself a full ship record: a guided projectile/fighter the
// firing ship spits out (e.g. a launched fighter from a fighter bay, or a heavy guided
// drone). Unlike SpawnFromShip (a lightweight ProjectileTable entry), this allocates a
// real ship slot, populates ~40 ship-record fields from the firing ship and the
// weapon's linked ship class, gives it a render node, points it at the firing ship's
// target, and starts it moving along the firing ship's heading. Returns 1 on success,
// 0 on failure (no free ship slot / node alloc failed), or the original firingShipPtr
// if weaponIndex == -1 (see the comment on that branch below).
public static class SpawnSpecialWeaponShip
{
    public static int Run(int firingShipPtr, short weaponIndex)
    {
        if (weaponIndex == -1)
        {
            // decompile 43133: param_1 is never reassigned on this path, so the
            // unconditional `return param_1` hands back the ORIGINAL firingShipPtr, not
            // 0 — both current callers only ever pass a valid 0..63 weapon-slot index,
            // so this is never observed in practice, but it's preserved bug-for-bug.
            return firingShipPtr;
        }

        ShipRec firing = ShipTable.FromPtr(firingShipPtr);

        short shipSlot = (short)AllocateShipSlot.Run(firing.CurrentSystem, 2);
        if (shipSlot == -1)
        {
            return 0;
        }

        // AllocateSpriteRecord is only called once the ship slot is confirmed free,
        // matching the decompile's short-circuited `(sVar3 == -1) || (... == 0)` — it
        // must not run (and leak an allocated node) on the failed-slot path.
        int nodeHandle = AllocateSpriteRecord.Run(0, 0, 0, 0);
        if (nodeHandle == 0)
        {
            return 0;
        }

        WeaponRecord weapon = Core.Model.GameData.Weapons[weaponIndex];
        ShipRec ship = ShipTable.Ships[shipSlot];

        var node = SpriteNodes.At(nodeHandle);
        node.UpdateUpp = SpriteNodeUppCells.ShipUpdateUpp;
        node.CollisionUpp = SpriteNodeUppCells.ShipDrawUpp;
        node.State = 1;
        node.UpdaterFlag = 1;
        node.ObjectPtr = ship.Ptr;
        node.SortKey = 10;

        ship.IsActive = 1;
        ship.HasWorldSpriteNode = 1;
        ship.CurrentSystem = firing.CurrentSystem;
        ship.PosX = firing.PosX;
        ship.PosY = firing.PosY;
        ship.VelX = firing.VelX;
        ship.VelY = firing.VelY;
        ship.ShipClass = (short)(weapon.AmmoLink - 128);
        ship.DefendedSpobIndex = -1;
        ship.IsTractored = 0;
        ship.HailQuoteSpoken = 0;
        ship.HasAfterburner = (byte)(HasAfterburner.Run(ship) ? 1 : 0);
        ship.IsCarriedFighter = 0;
        ship.SpawningMissionSlot = -1;
        ship.AiBehaviorType = ShipAiType.NavalFighter;
        ship.Govt = firing.Govt;
        ship.NavMode = -1;
        ship.JumpWindupTimer = 0;
        ship.AiState = ShipAiState.Idle;
        ship.AiManeuverState = ShipManeuverState.None;
        ship.AiActionTimer = 0;
        ship.SalvageClaimed = 0;
        ship.ProvokedFlag = 0;
        ship.DockedSpobIndex = -2;
        ship.PriorSystem = -2;

        var cls = Core.Model.GameData.ShipClasses[ship.ShipClass];
        ship.Shield = cls.Shield;   // numeric int->float copy, same convention as SpawnFleet/SpawnDudeShip/RefuelAndRepairEscorts
        ship.AiActionTimer = weapon.Lifetime;   // reuses the AI-hold-timer cell as this ship's self-destruct countdown
        ship.OwnerSlot = firing.SlotIndex;
        ship.Heading = firing.Heading;
        ship.PersIndex = -1;
        ship.DudeSpawnIndex = -1;
        ship.GrudgeMissionIndex = -1;
        if (firing.SlotIndex == 0)
        {
            ship.TargetSlot = -1;
        }
        else
        {
            ship.TargetSlot = firing.TargetSlot;
        }

        short inacc = weapon.Inaccuracy;
        if (0 < inacc)
        {
            short roll = (short)SeedEvoRng.Run((short)(inacc << 1));
            ship.Heading = (short)(ship.Heading + (roll - inacc));
        }

        double speedCap = ShipDerivedStats.EffectiveSpeed(ship);
        EvMath.AccelerateAlongHeading((double)weapon.ProjectileSpeed, speedCap, ship.Heading, ship);

        double ammoScale = firing.SlotIndex == 0 ? ShipStatConstants.Half : ShipStatConstants.NpcWeaponAmmoScale;
        for (short weaponSlot = 0; weaponSlot < ShipRecord.WeaponSlotCount; weaponSlot = (short)(weaponSlot + 1))
        {
            ship.WeaponSlotType[weaponSlot] = cls.DefaultWeaponType[weaponSlot];
            ship.WeaponSlotAmmo[weaponSlot] = (short)(int)(ammoScale * cls.DefaultWeaponAmmo[weaponSlot]);
        }

        // Drop a target that's this ship's owner, or shares its owner, back to none.
        if (ship.TargetSlot != -1 &&
            (ship.OwnerSlot == ship.TargetSlot ||
             (ship.OwnerSlot == Core.Model.GameData.Ships[ship.TargetSlot].OwnerSlot && ship.OwnerSlot != -1)))
        {
            ship.TargetSlot = -1;
        }

        ShipAi.ResetAiToIdle(ship);
        return 1;
    }
}
