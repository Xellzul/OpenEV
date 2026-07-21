// Port of FUN_10061460 (EV Override-11.c lines 40730-40792).

using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// Buffer note: eligible (local_38, char[4]) and cumWeight (local_34, short[12]) are simple indexed arrays — never address-taken or passed as
// Rect/Point/record pointers. Rendered as byte[] and short[] respectively; verified benign, no backing memory needed.
// acStack_8038 (char[32712] in decompile) is a decompile frame-padding artifact — never referenced in the function body; omitted.
// Only ever called with a dude record (param_1 = *0x1008a51c + dudeIndex*0x20) →
// takes the typed DudeSpawnRecord (field offsets documented there).
public static class PickWeightedSlot
{
    public static int Run(DudeSpawnRecord dude)
    {
        var eligible = new byte[DudeSpawnRecord.RollSlotCount];      // local_38
        var cumWeight = new short[12];   // local_34 — decompile-sized (compiler padding); only [0..RollSlotCount) used

        int totalWeight = 0;
        int chosenSlot = -1;
        for (short slot = 0; slot < DudeSpawnRecord.RollSlotCount; slot = (short)(slot + 1))
        {
            cumWeight[slot] = 0;
            if (dude.MissionBit[slot] == -1)
            {
                eligible[slot] = 1;
            }
            else
            {
                eligible[slot] = 0;
                if (dude.MissionBit[slot] < 0 || 0x1ff < dude.MissionBit[slot])   // 0x1ff = 9-bit control-bit-index max
                {
                    if (999 < dude.MissionBit[slot] &&
                        dude.MissionBit[slot] < 1512 &&
                        ControlBits.Get(dude.MissionBit[slot] - 1000) == 0)   // 1000.. = AliasBase spelling (see ControlBits)
                    {
                        eligible[slot] = 1;
                    }
                }
                else if (ControlBits.Get(dude.MissionBit[slot]) != 0)
                {
                    eligible[slot] = 1;
                }
            }
            if (eligible[slot] != 0)
            {
                totalWeight += dude.Weight[slot];
                for (short scanSlot = 0; scanSlot <= slot; scanSlot = (short)(scanSlot + 1))
                {
                    if (eligible[scanSlot] != 0)
                    {
                        cumWeight[slot] = (short)(cumWeight[slot] + dude.Weight[scanSlot]);
                    }
                }
            }
        }
        if ((short)totalWeight < 1)
        {
            return -1;
        }
        while ((short)chosenSlot == -1)
        {
            short roll = (short)SeedEvoRng.Run((short)totalWeight);
            for (int scanIndex = DudeSpawnRecord.RollSlotCount - 1; -1 < (short)scanIndex; scanIndex--)
            {
                short scanSlot = (short)scanIndex;
                if ((short)(roll + 1) <= cumWeight[scanSlot] &&
                    eligible[scanSlot] != 0 &&
                    -1 < dude.ShipClass[scanSlot])
                {
                    chosenSlot = scanIndex;
                }
            }
        }
        return chosenSlot;
    }
}
