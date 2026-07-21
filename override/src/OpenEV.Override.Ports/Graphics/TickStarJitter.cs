using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10023274 (EV Override-11.c lines 15386-15415): random-walk the
// starfield drift origin ±1 per axis per tick, clamped to [85, 115].
public static class TickStarJitter
{
    public static void Run()
    {
        var drift = WorldState.StarDrift;
        for (short axisIndex = 0; axisIndex < drift.Length; axisIndex = (short)(axisIndex + 1))
        {
            short jitterDir = (short)SeedEvoRng.Run(3);
            if (jitterDir == 0)
            {
                drift[axisIndex] = (short)(drift[axisIndex] - 1);
            }
            if (jitterDir == 1)
            {
                drift[axisIndex] = (short)(drift[axisIndex] + 1);
            }
            if (115 < drift[axisIndex])
            {
                drift[axisIndex] = 115;
            }
            if (drift[axisIndex] < 85)
            {
                drift[axisIndex] = 85;
            }
        }
    }
}
