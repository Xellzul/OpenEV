using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_1006c110 — EV Override-11.c lines 44416-44617. Spawns a "pers" (named
// character) ship: with persIndex == -1 it first scores which pers are eligible
// in the current system/govt context and picks one at random, otherwise it
// spawns the requested pers directly. Copies the pers' class, AI, and
// weapon/shield stats onto the new ship. Returns the ship slot, or -1.
public static class SpawnPers
{
    public static int Run(int systemIndex, byte suppressFlag, short persIndex)
    {
        byte[] eligible = new byte[PersTable.Count];
        short systemShort = (short)systemIndex;

        short eligibleCount = 0;
        if (persIndex == -1)
        {
            // Scans slots [0, Count-1) — the last pers slot is reserved for the
            // SpawnReinforcement escort entries and never auto-picked.
            //
            // pers.LinkSyst encodes where/when a pers can appear:
            //   -1            always eligible
            //   0..9999       a specific system (>=128 means syst index - 128)
            //   10000..14999  exactly this system's government
            //   15000..19999  a government allied with this system's government (either direction)
            //   20000..24999  any government except this system's government
            //   25000..29999  a government at war with this system's government (either direction)
            for (short i = 0; i < PersTable.Count - 1; i++)
            {
                eligible[i] = 0;
                var pers = GameData.Pers[i];
                if (pers.AvailableFlag != 0 && pers.AppearGate > 0)
                {
                    var sys = SystTable.Store[systemShort];
                    short link = pers.LinkSyst;
                    if (link == -1) eligible[i] = 1;
                    if (systemShort == link) eligible[i] = 1;
                    if (link > 127 && link < 10000 && systemShort == link - 128) eligible[i] = 1;
                    if (link > 9998 && link < 15000 && link - 10000 == sys.Govt) eligible[i] = 1;
                    if (link > 14999 && link < 20000 && sys.Govt > -1)
                    {
                        if (sys.Govt == GameData.Governments[link - 15000].Ally) eligible[i] = 1;
                        if (link - 15000 == GameData.Governments[sys.Govt].Ally) eligible[i] = 1;
                    }
                    if (link > 19999 && link < 25000 && sys.Govt > -1 && link - 20000 != sys.Govt) eligible[i] = 1;
                    if (link > 24999 && link < 30000 && sys.Govt > -1)
                    {
                        if (sys.Govt == GameData.Governments[link - 25000].Enemy) eligible[i] = 1;
                        if (link - 25000 == GameData.Governments[sys.Govt].Enemy) eligible[i] = 1;
                    }
                    // Suppress pers under a start-disabled/derelict government, when the
                    // caller asked to suppress them.
                    if (pers.Govt > -1 && (GameData.Governments[pers.Govt].Flags & GovtFlags.StartDisabledOrDerelict) != 0 && suppressFlag != 0)
                        eligible[i] = 0;
                    // Mission control-bit gate.
                    if (pers.AvailabilityBit != -1)
                    {
                        if (pers.AvailabilityBit < 1000)
                        {
                            if (ControlBits.Get(pers.AvailabilityBit) == 0) eligible[i] = 0;
                        }
                        else if (ControlBits.Get(pers.AvailabilityBit - 1000) != 0)
                        {
                            eligible[i] = 0;
                        }
                    }
                }
                if (eligible[i] != 0) eligibleCount++;
            }
        }
        else
        {
            eligibleCount = 1;
        }

        if (eligibleCount <= 0)
        {
            return -1;
        }

        if (persIndex == -1)
        {
            persIndex = (short)SeedEvoRng.Run(510);
        }
        else
        {
            eligible[persIndex] = 1;
            GameData.Pers[persIndex].AvailableFlag = 1;
        }

        if (eligible[persIndex] == 0)
        {
            return -1;
        }

        int result = AllocateShipSlot.Run(systemShort, 2);
        short slot = (short)result;
        if (slot == -1)
        {
            return -1;
        }

        var chosen = GameData.Pers[persIndex];
        var ship = ShipTable.Ships[slot];
        ship.PersIndex = persIndex;
        ship.ShipClass = chosen.ShipType;
        ship.DudeSpawnIndex = -1;
        ship.Govt = chosen.Govt;
        ship.AiBehaviorType = chosen.AppearGate;
        ship.AiCourage = chosen.AiCourage;
        ship.HailQuoteSpoken = 0;
        ship.HasAfterburner = (byte)(HasAfterburner.Run(ship) ? 1 : 0);
        if (((PersFlags)(ushort)chosen.Flags & PersFlags.PodAndAfterburner) != 0) ship.HasAfterburner = 1;
        if (ship.AiCourage < 1) ship.AiCourage = 1;
        if (ship.AiCourage > 2) ship.AiCourage = 4;

        var cls = GameData.ShipClasses[ship.ShipClass];
        for (int w = 0; w < ShipRecord.WeaponSlotCount; w++)
        {
            ship.WeaponSlotType[w] = (short)(cls.DefaultWeaponType[w] + chosen.WeaponType[w]);
            ship.WeaponSlotAmmo[w] = (short)(cls.DefaultWeaponAmmo[w] + chosen.WeaponAmmo[w]);
        }

        // The decompile truncates this product to int before storing it (fctiwz) — don't
        // drop the (int) cast to a bare float multiply, that would diverge from the original.
        ship.Shield = (int)(chosen.ShieldMultiplier * cls.Shield);
        if (ship.Govt != -1 && (GameData.Governments[ship.Govt].Flags & GovtFlags.StartDisabledOrDerelict) != 0)
        {
            // Suppressed-govt pers spawn pre-damaged: the armor-scale product overwrites
            // the Shield cell (it's armor, not shield), and velocity is zeroed.
            double armorScale = (cls.Flags & ShipFlags.DisabledAt10PctArmor) == 0
                ? ShipStatConstants.SpawnArmorScale
                : ShipStatConstants.SpawnArmorScaleTough;
            ship.Shield = (int)(armorScale * cls.BaseArmor);
            ship.VelY = ShipStatConstants.SpawnZeroDefault;
            ship.VelX = ShipStatConstants.SpawnZeroDefault;
        }

        ShipAi.ResetAiToIdle(ship);
        if (chosen.LinkMission != -1)
        {
            ResolveSingleMissionSpawn.Run(chosen.LinkMission);
        }
        return result;
    }
}
