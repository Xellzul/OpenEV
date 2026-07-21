using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Ship;

// Port of FUN_1006cc60 (EV Override-11.c lines 44623-44813).
//
// The player captures `cap` and moves aboard. If no free slot is available for the
// player's OLD ship the swap aborts (chatter + early return; the player does not board).
// Otherwise the old ship is moved into the new slot — kept as a player escort when
// abandonOldShip == 0, or abandoned as a disabled derelict (its cargo salvaged to the
// fleet) when abandonOldShip != 0 — the captured ship's identity/position/cargo are
// copied onto slot 0, and the player's weapons/outfits are rebuilt from the new hull
// (non-persistent outfits are lost).
public static class RunShipCaptureSwap
{
    public static void Run(ShipRec cap, byte abandonOldShip)
    {
        short slot = (short)AllocateShipSlot.Run(GameData.Player.CurrentSystem, 1);
        if (slot == -1)
        {
            EnqueueChatterEvent.Run("You were unable to retain your old ship as an escort.",
                                    240, 0, 12, UiColors.ChatterText, 0, 0);
            return;
        }

        if (cap.PersIndex != -1)
        {
            GameData.Pers[cap.PersIndex].AvailableFlag = 0;
        }

        // ── Clone the player's CURRENT ship into the new escort slot. ──
        var old = GameData.Ships[slot];
        var player = GameData.Player;
        old.ShipClass = player.ShipClass;
        old.DudeSpawnIndex = -1;
        old.PosX = player.PosX;
        old.PosY = player.PosY;
        old.VelX = player.VelX;
        old.VelY = player.VelY;
        old.Heading = player.Heading;
        old.HeadingPrev = old.Heading;
        old.NavTargetSpob = -1;
        old.TargetSlot = -1;
        old.SelectedWeaponSlot = -1;
        old.CurrentSystem = player.CurrentSystem;
        old.Govt = -1;
        old.IsCarriedFighter = 0;
        old.HailQuoteSpoken = 0;
        old.HasAfterburner = (byte)(HasAfterburner.Run(ShipTable.Ships[slot]) ? 1 : 0);
        old.SpawningMissionSlot = -1;
        old.SlotIndex = slot;
        for (short i = 0; i < ShipRecord.CargoHoldCount; i++)
        {
            old.CargoHold[i] = player.CargoHold[i];
        }

        // Release any escorts that were following the captured ship.
        for (short i = 1; i < ShipTable.Count; i++)
        {
            var esc = GameData.Ships[i];
            if (esc.IsActive != 0 && i != cap.SlotIndex && cap.SlotIndex == esc.OwnerSlot)
            {
                esc.NavTargetSpob = -1;
                esc.TargetSlot = -1;
                esc.ProvokedFlag = 0;
                esc.OwnerSlot = -1;
                esc.AiBehaviorType = GameData.ShipClasses[esc.ShipClass].InherentAI;
            }
        }

        if (abandonOldShip == 0)
        {
            // Kept as a player escort: player-owned (OwnerSlot 0), hired-escort AI (6),
            // Shield 0, full fuel.
            old.Shield = 0;
            old.OwnerSlot = 0;
            short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Ships[slot]);
            old.Fuel = (float)fuelMax;
            old.AiBehaviorType = ShipAiType.Escort;
        }
        else
        {
            // Abandoned as a disabled derelict: (1 - armor) clamped, stored as NEGATIVE
            // Shield (+0x68 = armor damage) — NOT Credits (+0x60); its cargo is salvaged to
            // the fleet, then it is left ownerless (OwnerSlot -1) and inactive (AI -1).
            // DEAD in the shipping game: the sole caller (ShowBoardingDialog, sub_6CC60's
            // only XREF) passes 0, so abandonOldShip is never nonzero. Ported faithfully.
            old.Shield = 1 - GameData.ShipClasses[old.ShipClass].BaseArmor;
            if (-1 < old.Shield)
            {
                old.Shield = -32767;
            }
            old.SalvageClaimed = 1;
            old.IsCarriedFighter = 0;
            old.HailQuoteSpoken = 0;
            old.HasAfterburner = (byte)(HasAfterburner.Run(ShipTable.Ships[slot]) ? 1 : 0);
            old.OwnerSlot = 0;
            old.AiBehaviorType = ShipAiType.Escort;
            RedistributeCargoAmongShips.Run(slot);
            old.OwnerSlot = -1;
            old.AiBehaviorType = ShipAiType.Inactive;
        }
        WorldState.HudStatusPanelDirty = 1;
        WorldState.WeaponSlotDirty = 1;
        var oldCls = GameData.ShipClasses[old.ShipClass];
        for (short i = 0; i < ShipRecord.WeaponSlotCount; i++)
        {
            old.WeaponSlotType[i] = oldCls.DefaultWeaponType[i];
            old.WeaponSlotAmmo[i] = oldCls.DefaultWeaponAmmo[i];
        }
        int spawnAngle = (int)SeedEvoRng.Run(360);
        EvMath.OffsetByHeading(ShipStatConstants.CaptureSpawnDist, spawnAngle,
                               ref old.PosX, ref old.PosY);

        // ── Move the player aboard the captured ship. ──
        player.ShipClass = cap.ShipClass;
        player.DudeSpawnIndex = cap.DudeSpawnIndex;
        player.PosX = cap.PosX;
        player.PosY = cap.PosY;
        player.VelX = cap.VelX;
        player.VelY = cap.VelY;
        player.Heading = cap.Heading;
        player.HeadingPrev = player.Heading;
        player.NavTargetSpob = -1;
        player.TargetSlot = -1;
        player.SelectedWeaponSlot = -1;
        for (short i = 0; i < ShipRecord.CargoHoldCount; i++)
        {
            player.CargoHold[i] = cap.CargoHold[i];
        }
        player.Shield = 0;
        // Random starting fuel in [0, fuelMax) for the swapped-in ship. The decompile's
        // FUN_1005d9c4() shows no arg because r3 still holds EffectiveFuelMax's return.
        short fuelRoll = (short)SeedEvoRng.Run((short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Player));
        player.Fuel = (float)fuelRoll;
        player.AiBehaviorType = ShipAiType.Inactive;
        player.OwnerSlot = -1;
        player.Govt = -1;

        // Weapons granted by a PERSISTENT weapon-mod outfit survive the swap; all
        // other weapon slots are zeroed before the new hull's stock is added back.
        for (short wSlot = 0; wSlot < ShipRecord.WeaponSlotCount; wSlot++)
        {
            bool hasPersistentMod = false;
            for (short i = 0; i < OutfitTable.Count; i++)
            {
                var outfit = OutfitTable.Store[i];
                if (outfit.ModType[0] == OutfitModType.Weapon && wSlot == outfit.ModValue[0] &&
                    outfit.PersistentFlagSet != 0)
                {
                    hasPersistentMod = true;
                    break;
                }
                if (outfit.ModType[1] == OutfitModType.Weapon && wSlot == outfit.ModValue[1] &&
                    outfit.PersistentFlagSet != 0)
                {
                    hasPersistentMod = true;
                    break;
                }
            }
            if (!hasPersistentMod)
            {
                player.WeaponSlotType[wSlot] = 0;
                player.WeaponSlotAmmo[wSlot] = 0;
            }
        }
        // Drop all non-persistent owned outfits, then grant the new hull's defaults.
        for (short i = 0; i < OutfitTable.Count; i++)
        {
            if (OutfitTable.Store[i].PersistentFlagSet == 0)
            {
                OwnedOutfitGrid.Store[i] = 0;
            }
        }
        var newCls = GameData.ShipClasses[player.ShipClass];
        for (short i = 0; i < ShipClassRecord.DefaultItemSlots; i++)
        {
            if (0 < newCls.DefaultItemsCount[i])
            {
                OwnedOutfitGrid.Store[newCls.DefaultItems[i]] =
                    (short)(OwnedOutfitGrid.Store[newCls.DefaultItems[i]] + newCls.DefaultItemsCount[i]);
            }
        }
        RebuildMarketFromOwnedOutfits.Run();
        for (short i = 0; i < ShipRecord.WeaponSlotCount; i++)
        {
            player.WeaponSlotType[i] = (short)(player.WeaponSlotType[i] + newCls.DefaultWeaponType[i]);
            player.WeaponSlotAmmo[i] = (short)(player.WeaponSlotAmmo[i] + newCls.DefaultWeaponAmmo[i]);
        }

        cap.IsActive = 0;
        cap.HasWorldSpriteNode = 0;
        EnqueueChatterEvent.Run("You retained your old ship as an escort.",
                                240, 0, 12, UiColors.ChatterText, 0, 0);
    }
}
