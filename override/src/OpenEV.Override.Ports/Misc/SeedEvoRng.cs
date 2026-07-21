using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1005d9c4 (EV Override-11.c 38838-38868): EVO's Lehmer/MINSTD RNG
// (multiplier 16807 = 7^5). range == 0 reseeds the state from the Mac clock
// (LMGetTime + LMGetTicks); range != 0 advances the generator and returns a value in
// [0, range).
//
// The generator state lives in managed memory as GameData.EvoRngState (uint; was the raw
// _DAT_10082218 cell). Decompile 38862's `state >> 16` is a LOGICAL shift because the state
// is uint-typed there — keep the field unsigned, or the shift silently turns arithmetic.
public static class SeedEvoRng
{
    public static uint Run(short range)
    {
        if (range == 0)
        {
            // Reseed from the clock. (The range == 0 path returns the untracked register
            // unaff_r27, never written here — defaults to 0.)
            GameData.EvoRngState = (uint)(MacToolbox.LMGetTime() + MacToolbox.LMGetTicks());
            return 0;
        }

        uint state = GameData.EvoRngState;
        uint seedLow = (state & 0xffff) * 16807;
        uint seedHigh = (state >> 16) * 16807 + (seedLow >> 16);
        state = (seedLow & 0xffff) + 0x80000001 + (seedHigh & 0x7fff) * 0x10000 + (seedHigh * 2 >> 16);
        GameData.EvoRngState = state;

        uint roll = (state & 0xffff) == 0x8000 ? 0 : state;
        // Decompile 38862: `(int)param_1 * (uVar3 & 0xffff) >> 0x10` — `uVar3 & 0xffff` is uint,
        // so this must stay an UNSIGNED multiply (mod 2^32) with a LOGICAL `>> 16` shift; range
        // is cast to int first (sign-extend), then to uint, before the multiply.
        return (uint)(int)range * (roll & 0xffff) >> 16;
    }
}
