namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Ship.Model;

// Port of FUN_1005e150 (EV Override-11.c 39064-39085) — is there room for another escort?
// Counts the player's active escorts across ship slots 1..35 — slot occupied, owned by the
// player (OwnerSlot 0), escort-follow AI (AiBehaviorType 6), no grudge — and reports whether
// fewer than 6 are present.
public static class EscortRoomAvailable
{
    public static bool Run()
    {
        short escortCount = 0;
        for (short slotIndex = 1; slotIndex < ShipTable.Count; slotIndex++)
        {
            var ship = ShipTable.Ships[slotIndex];
            if (ship.IsActive != 0 &&
                ship.OwnerSlot == 0 &&
                ship.AiBehaviorType == ShipAiType.Escort &&
                ship.GrudgeMissionIndex == -1)
            {
                escortCount++;
            }
        }
        return escortCount < 6;
    }
}
