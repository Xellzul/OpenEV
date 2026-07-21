namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Core.Model;

// FUN_10022f44 — EV Override-11.c lines 15265-15294. Finds the first free beam slot (Life < -1)
// and populates it for a newly-fired beam weapon; does nothing if all BeamTable.Count slots are busy.
public static class AllocateBeamSlot
{
    // `heading` is the firing angle the callers pass; this function ignores it (faithful to the original).
    public static void Run(short ownerIndex, short targetIndex, short weaponType, int heading,
                           byte sourceShip, short fixedRange)
    {
        short beamSlot = 0;
        while (beamSlot < BeamTable.Count && GameData.Beams[beamSlot].Life >= -1)
            beamSlot++;
        if (beamSlot >= BeamTable.Count)
            return;

        var weapon = GameData.Weapons[weaponType];
        var beam = GameData.Beams[beamSlot];
        beam.Life = weapon.Lifetime;
        beam.WeaponType = weaponType;
        beam.SourceShip = sourceShip;
        beam.OwnerSlot = ownerIndex;
        beam.FixedRange = fixedRange;
        beam.TargetSlot = weapon.GuidanceType == 3 ? targetIndex : (short)-1;
    }
}
