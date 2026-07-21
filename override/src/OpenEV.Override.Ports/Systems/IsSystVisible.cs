using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
namespace OpenEV.Override.Ports.Systems;

// Port of FUN_10051e9c (EV Override-11.c lines 33546-33572).
public static class IsSystVisible
{
    // Returns int (not bool), though every ASM path returns literal 0/1: ResolveSystSentinel.cs
    // reuses the result inside a dual-purpose scratch/return-sentinel int local (masked with
    // & 0xff, cast to byte, and returned directly as that function's own int return) — converting
    // this signature would force restructuring that not-yet-cleaned caller's data flow.
    public static int Run(short systIndex)
    {
        if (SystTable.ShownFlag(systIndex) == 0)
        {
            return 0;
        }
        short visibility = SystTable.Visibility(systIndex);
        if (visibility != -1)
        {
            if (visibility < 512 && -1 < visibility)
            {
                // Low band: bit must be SET to stay visible.
                if (!ControlBits.IsSet(visibility))
                {
                    return 0;
                }
            }
            // High band: bit must be CLEAR to stay visible.
            else if (999 < visibility && visibility < 1512 &&
                     ControlBits.IsSet(visibility - 1000))
            {
                return 0;
            }
        }
        return 1;
    }
}
