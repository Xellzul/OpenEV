using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Ship;

// Port of FUN_10058ae0 (EV Override-11.c 36459-36536).
// Target-cycling: returns the next selectable ship slot after `start` in system
// `systIndex`, or `start` when none qualifies. With start == -1 it scans slots
// 1..35 from the beginning. A player-owned or player-escort ship (OwnerSlot == 0,
// or flagged escortable below) is skipped as a target unless the Option key is
// held, the ship is hireable, or it defends a spob (DefendedSpobIndex set).
public static class FindNextShipSlot
{
    public static int Run(int start, short systIndex)
    {
        // BUG FIX (Pass-1 mis-rendering, restores ASM fidelity): decompile passes
        // FUN_1005f964(0x32) — a keymap-space index; real key is 0x32^8 = Option, not
        // Grave. See Keymap.TestLiveKeymapBit's "Caller keycode space" note. Don't
        // revert to Grave.
        bool optionHeld = Keymap.TestLiveKeymapBit(MacKeycode.Option) != 0;
        bool[] escortable = new bool[ShipTable.Count];
        bool[] hireable = new bool[ShipTable.Count];

        for (int slot = 1; slot < ShipTable.Count; slot++)
        {
            var ship = ShipTable.Ships[slot];
            if (ship.GrudgeMissionIndex == -1)
            {
                // Escortable: this ship defends no spob and follows a leader (OwnerSlot) that is itself player-owned.
                if (ship.OwnerSlot != -1 && ship.OwnerSlot != 0 &&
                    ship.DefendedSpobIndex == -1 &&
                    ShipTable.Ships[ship.OwnerSlot].OwnerSlot == 0)
                {
                    escortable[slot] = true;
                }
            }
            else if (GameData.MissionStates[ship.GrudgeMissionIndex].IsActive != 0)
            {
                // ShipBehavior reduced by 10s while > 8; result 1 marks a hireable ship.
                short shipBehavior = GameData.Missions[ship.GrudgeMissionIndex].ShipBehavior;
                while (8 < shipBehavior)
                {
                    shipBehavior -= 10;
                }
                if (shipBehavior == 1)
                {
                    hireable[slot] = true;
                }
            }
        }

        if ((short)start == -1)
        {
            for (int i = 1; i < ShipTable.Count; i++)
            {
                var ship = ShipTable.Ships[i];
                if (ship.IsActive == 0 || ShipDerivedStats.IsDyingOrDestroyed(ship))
                {
                    continue;
                }
                if (systIndex != ship.CurrentSystem)
                {
                    continue;
                }
                bool skip = (ship.OwnerSlot == 0 || escortable[i]) &&
                            !optionHeld && !hireable[i] && ship.DefendedSpobIndex == -1;
                if (!skip)
                {
                    return i;
                }
            }
            return start;
        }

        for (int i = start + 1; (short)i < ShipTable.Count; i++)
        {
            var ship = ShipTable.Ships[i];
            if (ship.IsActive != 0 &&
                !ShipDerivedStats.IsDyingOrDestroyed(ship) &&
                systIndex == ship.CurrentSystem)
            {
                if (ship.OwnerSlot != 0 && !escortable[i])
                {
                    return i;
                }
                if (optionHeld)
                {
                    return i;
                }
                if (hireable[i])
                {
                    return i;
                }
                if (ship.DefendedSpobIndex != -1)
                {
                    return i;
                }
            }
        }
        return start;
    }
}
