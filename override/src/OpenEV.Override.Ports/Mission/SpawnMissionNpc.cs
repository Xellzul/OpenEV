using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_100664bc (EV Override-11.c lines 42589-42664). Spawns one NPC ship
// for missionIndex's dude-spawn table entry (dudeIndex) into systemIndex, rolled
// and initialised the same way as the sibling SpawnDudeShip / SpawnSystArrivalNpc
// ports; returns the ship slot, or -1.
public static class SpawnMissionNpc
{
    public static int Run(short dudeIndex, short systemIndex, short missionIndex)
    {
        int slotResult = AllocateShipSlot.Run(systemIndex, 1);
        short slot = (short)slotResult;
        if (slot == -1)
            return -1;

        SeedEvoRng.Run(100);
        var dude = GameData.DudeSpawns[dudeIndex];
        short classRoll = (short)PickWeightedSlot.Run(dude);
        if (classRoll < 0 || classRoll >= DudeSpawnRecord.RollSlotCount)
        {
            GameData.Ships[slot].IsActive = 0;
            GameData.Ships[slot].HasWorldSpriteNode = 0;
            return -1;
        }

        GameData.Ships[slot].GrudgeMissionIndex = missionIndex;
        GameData.Ships[slot].DudeSpawnIndex = dudeIndex;
        GameData.Ships[slot].ShipClass = dude.ShipClass[classRoll];
        GameData.Ships[slot].Govt = dude.Govt;
        GameData.Ships[slot].SalvageClaimed = 0;
        GameData.Ships[slot].AiActionTimer = 0;

        var npcCls = GameData.ShipClasses[GameData.Ships[slot].ShipClass];
        GameData.Ships[slot].AiBehaviorType = dude.AiType < ShipAiType.WimpyTrader ? npcCls.InherentAI : dude.AiType;
        ShipAi.ResetAiToIdle(ShipTable.Ships[slot]);

        short shipBehavior = GameData.Missions[missionIndex].ShipBehavior;
        for (int weaponSlot = 0; weaponSlot < ShipRecord.WeaponSlotCount; weaponSlot++)
        {
            GameData.Ships[slot].WeaponSlotType[weaponSlot] = npcCls.DefaultWeaponType[weaponSlot];
            if ((shipBehavior == 0 || shipBehavior == 10) &&
                (WeaponGuidanceType)GameData.Weapons[weaponSlot].GuidanceType != WeaponGuidanceType.CarriedShip)
            {
                GameData.Ships[slot].WeaponSlotAmmo[weaponSlot] =
                    (short)(int)(ShipStatConstants.NpcWeaponAmmoScale * npcCls.DefaultWeaponAmmo[weaponSlot]);
            }
            else
            {
                GameData.Ships[slot].WeaponSlotAmmo[weaponSlot] = npcCls.DefaultWeaponAmmo[weaponSlot];
            }
        }

        // Shield holds a genuine numeric value here (see ShipClassRecord.Shield), not a
        // bit-pattern reinterpret.
        GameData.Ships[slot].Shield = npcCls.Shield;

        if (shipBehavior == 0 || shipBehavior == 10)
        {
            ShipAi.CallForDefendersAndEngagePlayer(ShipTable.Ships[slot]);
        }

        return slotResult;
    }
}
