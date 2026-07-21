using System;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Ship;

// Ship-derived stats and status predicates (capacity, speed, mass, disabled/active
// status, weapon readiness). The AI behaviour — state predicates, state-mutating
// actions, engagement and the per-frame dispatcher — lives in the sibling ShipAi.
// Each method corresponds to a single decompiled function.
public static class ShipDerivedStats
{
    // FUN_1005aeb4 — EV Override-11.c lines 37475-37497.
    public static bool HasDensityScanner(ShipRec ship)
        => ship.SlotIndex == 0 && OutfitTable.PlayerHasOutfit(OutfitModType.DensityScanner);

    // FUN_1005af64 — EV Override-11.c lines 37498-37520.
    public static bool HasIffRadar(ShipRec ship)
        => ship.SlotIndex == 0 && OutfitTable.PlayerHasOutfit(OutfitModType.IffRadar);

    // FUN_1005ae04 — EV Override-11.c lines 37452-37474.
    public static bool HasCloakingDevice(ShipRec ship)
        => ship.SlotIndex == 0 && OutfitTable.PlayerHasOutfit(OutfitModType.CloakingDevice);

    // FUN_1005a7e0 — EV Override-11.c lines 37296-37318.
    public static bool HasAutoEject(ShipRec ship)
        => ship.SlotIndex == 0 && OutfitTable.PlayerHasOutfit(OutfitModType.AutoEject);

    // FUN_1005a730 — EV Override-11.c lines 37273-37295.
    public static bool HasEscapePod(ShipRec ship)
        => ship.SlotIndex == 0 && OutfitTable.PlayerHasOutfit(OutfitModType.EscapePod);

    // FUN_1005a890 — EV Override-11.c lines 37319-37341.
    public static bool HasAfterburner(ShipRec ship)
        => ship.SlotIndex == 0 && OutfitTable.PlayerHasOutfit(OutfitModType.Afterburner);

    // FUN_1005a0d8 — EV Override-11.c lines 37088-37107.
    public static int EffectiveArmorMax(ShipRec ship)
    {
        int armor = GameData.ShipClasses[ship.ShipClass].BaseArmor;
        if (ship.SlotIndex == 0)
            // no 0<owned guard in FUN_1005a0d8 (unlike the Fuel sibling) — a negative owned count subtracts
            armor += OutfitTable.SumOutfitModValue(OutfitModType.Armor, guardOwnedPositive: false);
        return armor;
    }

    // FUN_10059ddc — EV Override-11.c lines 36995-37019. True if the ship is in its death
    // sequence (DeathTimer running) or destroyed (shield past the negative-armor death line);
    // callers use !IsDyingOrDestroyed to mean "a live, targetable ship".
    public static bool IsDyingOrDestroyed(ShipRec ship)
    {
        if (ship.DeathTimer > ShipStatConstants.NearestSearchEpsilon)
            return true;

        int shield = (int)ship.Shield;
        // EffectiveArmorMax is computed only when shield < 0, per the decompile's short-circuit
        if (shield < 0 && shield < -(short)EffectiveArmorMax(ship))
            return true;
        return false;
    }

    // FUN_1005a1a0 — EV Override-11.c lines 37113-37133.
    public static int EffectiveFuelMax(ShipRec ship)
    {
        int fuel = GameData.ShipClasses[ship.ShipClass].BaseFuel;
        if (ship.SlotIndex == 0)
            fuel += OutfitTable.SumOutfitModValue(OutfitModType.Fuel);
        return fuel;
    }

    // FUN_10059e54 — EV Override-11.c lines 37020-37044. Effective shield capacity:
    // base class shield plus the player's Shield outfit mods, or an NPC pers scale.
    public static uint EffectiveShieldMax(ShipRec ship)
    {
        uint shieldMax = (uint)GameData.ShipClasses[ship.ShipClass].Shield;
        if (ship.SlotIndex == 0)
            shieldMax += (uint)OutfitTable.SumOutfitModValue(OutfitModType.Shield);
        else if (ship.PersIndex != -1)
            // the ASM's fctiwz converts the product to a SIGNED int first — do not collapse
            // (uint)(int) to (uint): .NET float->uint saturates a negative product to 0
            shieldMax = (uint)(int)((float)(int)shieldMax * GameData.Pers[ship.PersIndex].ShieldMultiplier);
        return shieldMax;
    }

    // FUN_10059f70 — EV Override-11.c lines 37050-37082. Shield-recharge rate (÷TimeScale,
    // clamped ≥1): base class rate plus per-bank ShieldRecharge outfit mods for
    // the player, or an NPC maneuver scale. (Accumulator is short, per the decompile.)
    public static int EffectiveShieldRecharge(ShipRec ship)
    {
        short recharge = GameData.ShipClasses[ship.ShipClass].ShieldRecharge;
        if (ship.SlotIndex == 0)
        {
            for (int slot = 0; slot < OutfitTable.Count; slot++)
            {
                var outfit = GameData.Outfits[slot];
                for (int bank = 0; bank < OutfitRecord.ModBankCount; bank++)
                    if (outfit.ModType[bank] == OutfitModType.ShieldRecharge && OwnedOutfitGrid.Store[slot] > 0)
                        recharge = (short)(recharge + outfit.ModValue[bank] * OwnedOutfitGrid.Store[slot]);
            }
        }
        else if (ship.OwnerSlot != 0)
        {
            recharge = (short)(int)(recharge * ShipPhysicsConstants.ShipManeuverScale);
        }
        int result = (int)(recharge / WorldState.TimeScale);
        if ((short)result < 1)
            result = 1;
        return result;
    }

    // FUN_1005a40c — EV Override-11.c lines 37180-37223. Effective acceleration: base
    // class accel plus per-bank Acceleration outfit mods for the player (each ÷ the
    // accel divisor), or an NPC field scale; × the speed/accel scale, with a final pers
    // boost.
    public static double EffectiveAccel(ShipRec ship)
    {
        float accel = GameData.ShipClasses[ship.ShipClass].Accel;
        double result;
        if (ship.SlotIndex == 0)
        {
            for (int slot = 0; slot < OutfitTable.Count; slot++)
            {
                var outfit = GameData.Outfits[slot];
                for (int bank = 0; bank < OutfitRecord.ModBankCount; bank++)
                    if (outfit.ModType[bank] == OutfitModType.Acceleration)
                    {
                        float perUnit = (float)(outfit.ModValue[bank] / ShipPhysicsConstants.ShipAccelDivisor);
                        float owned = OwnedOutfitGrid.Store[slot];
                        accel += perUnit * owned;
                    }
            }
            result = accel * ShipPhysicsConstants.ShipSpeedAccelScale;
        }
        else
        {
            accel *= ship.PilotSkillScale;
            // decompile 37208-37213 also branches on OwnerSlot (+0x5e) here — vacuous: both arms identical
            result = ship.IsTractored == 0
                ? accel * ShipPhysicsConstants.ShipSpeedAccelScale
                : accel * ShipPhysicsConstants.ShipSpeedScaleAlt;
        }
        if (ship.PersIndex == ShipRecord.KamikazePersIndex)
            result = (float)(result * ShipPhysicsConstants.ShipSpeedAccelScale);
        return result;
    }

    // FUN_1005a59c — EV Override-11.c lines 37229-37267. Effective top speed: base class
    // speed plus per-bank Speed outfit mods for the player (each ÷ the speed-mod divisor),
    // or an NPC field scale; × pers boost, then a final non-combat speed scale.
    public static double EffectiveSpeed(ShipRec ship)
    {
        double speed = GameData.ShipClasses[ship.ShipClass].Speed;
        if (ship.SlotIndex == 0)
        {
            for (int slot = 0; slot < OutfitTable.Count; slot++)
            {
                var outfit = GameData.Outfits[slot];
                for (int bank = 0; bank < OutfitRecord.ModBankCount; bank++)
                    if (outfit.ModType[bank] == OutfitModType.Speed)
                    {
                        float perUnit = (float)(outfit.ModValue[bank] / ShipPhysicsConstants.ShipSpeedModDivisor);
                        float owned = OwnedOutfitGrid.Store[slot];
                        speed = (float)(speed + perUnit * owned);
                    }
            }
        }
        else
        {
            speed = (float)(speed * ship.PilotSkillScale);
            if (ship.IsTractored != 0)
                speed = (float)(speed * ShipPhysicsConstants.ShipSpeedScaleAlt);
        }
        if (ship.PersIndex == ShipRecord.KamikazePersIndex)
            speed = (float)(speed * ShipPhysicsConstants.ShipSpeedAccelScale);
        if ((ship.SlotIndex == 0 || ship.OwnerSlot == 0) && WorldState.StrictPlay == 0)
            speed = (float)(speed * ShipPhysicsConstants.ShipStatFinalScale);
        return speed;
    }

    // FUN_100593dc — EV Override-11.c lines 36702-36720.
    public static int EffectiveCargoMax()
    {
        int cargo = GameData.ShipClasses[GameData.Player.ShipClass].Holds;
        // no 0<owned guard in FUN_100593dc — adds unconditionally
        cargo += OutfitTable.SumOutfitModValue(OutfitModType.Cargo, guardOwnedPositive: false);
        return cargo;
    }

    // FUN_1005b134 — EV Override-11.c lines 37556-37579. Squared hyperspace-jump range:
    // base 1000 plus the player's HyperRange outfit mods, clamped ≥0, then squared.
    public static int EffectiveHyperRangeSquared(ShipRec ship)
    {
        int hyperRange = 1000;
        if (ship.SlotIndex == 0)
            hyperRange += OutfitTable.SumOutfitModValue(OutfitModType.HyperRange);
        if (hyperRange < 0)
            hyperRange = 0;
        return hyperRange * hyperRange;
    }

    // FUN_10060974 — EV Override-11.c lines 40415-40432.
    public static int InterferenceReduction()
        // no 0<owned guard in FUN_10060974 — adds unconditionally
        => OutfitTable.SumOutfitModValue(OutfitModType.InterferenceReduction, guardOwnedPositive: false);

    // FUN_10059b54 — EV Override-11.c lines 36908-36927.
    public static int FreeMassSpace()
    {
        int free = GameData.ShipClasses[GameData.Player.ShipClass].FreeMass;
        for (int slot = 0; slot < OutfitTable.Count; slot++)
        {
            short owned = OwnedOutfitGrid.Store[slot];
            if (owned > 0)
                free -= GameData.Outfits[slot].Mass * owned;
        }
        return free;
    }

    // FUN_100592d4 — EV Override-11.c lines 36672-36700.
    // Total cargo mass aboard: the 6 commodity holds, plus (player only) picked-up
    // mission cargo and held junk commodities.
    public static int TotalMassCarried(ShipRec ship)
    {
        int totalMass = 0;
        for (int i = 0; i < ShipRecord.CargoHoldCount; i++)
            totalMass += ship.CargoHold[i];
        if (ship.SlotIndex == 0)
        {
            for (int i = 0; i < MissionTable.Count; i++)
                if (GameData.MissionStates[i].IsActive != 0 &&
                    GameData.Missions[i].CargoPickedUp != 0)
                    totalMass += GameData.Missions[i].CargoMass;
            for (int i = 0; i < JunkTable.Count; i++)
                if (GameData.Junk[i].PlayerQty > 0)
                    totalMass += GameData.Junk[i].PlayerQty;
        }
        return totalMass;
    }

    // FUN_10061760 — EV Override-11.c lines 40809-40826.
    public static bool IsPlayerOrEscort(ShipRec ship)
    {
        if (ship.SlotIndex == 0)
            return true;
        short ownerSlot = ship.OwnerSlot;
        if (ownerSlot == 0)
            return true;
        if (ownerSlot != -1 && GameData.Ships[ownerSlot].OwnerSlot == 0)
            return true;
        return false;
    }

    // FUN_10059c58 — EV Override-11.c lines 36947-36994.
    public static bool IsDisabled(ShipRec ship)
    {
        if (ship.SlotIndex == 0)
            return false;
        if (ship.Govt != -1 &&
            (GameData.Governments[ship.Govt].Flags & GovtFlags.StartDisabledOrDerelict) != 0)
            return true;
        if (ship.DefendedSpobIndex != -1)
            return false;
        int shield = (int)ship.Shield;
        if (shield < 0)
        {
            short maxArmor = (short)EffectiveArmorMax(ship);
            double scale = (GameData.ShipClasses[ship.ShipClass].Flags & ShipFlags.DisabledAt10PctArmor) == 0
                ? ShipStatConstants.DisableArmorScaleStd
                : ShipStatConstants.DisableArmorScaleTough;
            if (shield * 100 < scale * maxArmor)
                return true;
        }
        return false;
    }

    // FUN_1005e218 — EV Override-11.c lines 39086-39107.
    // Can the ship fire its weapon slot `weaponIndex`? True when the weapon needs
    // no ammo (AmmoLink < 0) or has rounds loaded; but a fuel-cost weapon (AmmoLink
    // ≤ -1000, encoding a fuel cost of |AmmoLink|-1000) is blocked when the ship
    // lacks the fuel.
    public static bool CanFireWeapon(ShipRec ship, short weaponIndex)
    {
        short ammoLink = GameData.Weapons[weaponIndex].AmmoLink;
        if (ammoLink >= 0 && ship.WeaponSlotAmmo[weaponIndex] <= 0)
            return false;
        if (ammoLink < -999 && ship.Fuel < (float)(Math.Abs((int)ammoLink) - 1000))
            return false;
        return true;
    }

    // FUN_10005b48 — EV Override-11.c lines 3516-3548. Is system `systIndex` a
    // permitted destination for this ship? Allowed when the system has no stellar
    // links set, or one of its links matches the ship's DockedSpobIndex.
    public static bool IsDestinationAllowedBySyst(ShipRec ship, short systIndex)
    {
        int linkCount = 0;
        for (int slot = 0; slot < SystRecord.StellarLinkCount; slot++)
            if (SystTable.SpobLink(systIndex, slot) != -1)
                linkCount++;
        if (linkCount == 0)
            return true;
        for (int slot = 0; slot < SystRecord.StellarLinkCount; slot++)
        {
            short link = SystTable.SpobLink(systIndex, slot);
            if (ship.DockedSpobIndex == link && link != -1)
                return true;
        }
        return false;
    }

    // FUN_1006b280 — EV Override-11.c lines 44094-44109. True if any live NPC ship
    // (slots 1..35) is defending spob `spobIndex` (its DefendedSpobIndex matches) —
    // e.g. a tribute/spaceport defender is already assigned there.
    public static bool AnyShipDefendingSpob(short spobIndex)
    {
        for (int i = 1; i < ShipTable.Count; i++)
        {
            var ship = GameData.Ships[i];
            if (ship.IsActive != 0 && ship.DefendedSpobIndex != -1 && spobIndex == ship.DefendedSpobIndex)
                return true;
        }
        return false;
    }

    // FUN_1005a27c — EV Override-11.c lines 37139-37174. The ship's effective turn
    // rate (degrees/frame, ×angle-scale): base class Maneuver, plus outfit mods for
    // the player or an NPC scale factor, +1 for the special pers, clamped ≥1.
    public static int EffectiveManeuver(ShipRec ship)
    {
        var cls = GameData.ShipClasses[ship.ShipClass];
        short maneuver = cls.Maneuver;
        if (ship.SlotIndex == 0)
            // no 0<owned guard in FUN_1005a27c — adds unconditionally. (The decompile renders the
            // accumulator short; the ASM accumulates 32-bit and truncates at the use sites —
            // bit-identical, since mod-2^16 truncation commutes over +/×.)
            maneuver = (short)(maneuver + OutfitTable.SumOutfitModValue(OutfitModType.Maneuver, guardOwnedPositive: false));
        else if (ship.IsTractored != 0)
            maneuver = (short)(int)(maneuver * ShipPhysicsConstants.NonPlayerManeuverScale);

        if (ship.PersIndex == ShipRecord.KamikazePersIndex && maneuver < 6)
            maneuver += 1;

        int scaled = (int)(maneuver * WorldState.TimeScale);
        // final clamp re-reads the class's base Maneuver (not the modded accumulator), per the decompile
        if (cls.Maneuver > 0 && (short)scaled < 1)
            scaled = 1;
        return scaled;
    }
}
