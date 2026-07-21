using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10023358 (EV Override-11.c lines 15416-15434): reseed both star-jitter
// axes to rand(21) + 90 (range 90..110). The short[2] formerly behind PTR_DAT_10080ddc
// is Core.Model.WorldState.StarJitter now (pilot files round-trip the
// pair at aux-record offset 0x22fa).
public static class ReseedStarJitter
{
    public static void Run()
    {
        for (short axisIndex = 0; axisIndex < WorldState.StarJitter.Length; axisIndex = (short)(axisIndex + 1))
        {
            short randOffset = (short)SeedEvoRng.Run(21);
            WorldState.StarJitter[axisIndex] = (short)(randOffset + 90);
        }
    }
}
