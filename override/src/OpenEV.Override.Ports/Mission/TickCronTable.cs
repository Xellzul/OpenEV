using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1006d70c (EV Override-11.c lines 44820-44914).
//
// Daily cron advance: counts down active events; for idle events with no
// control-bit link (ControlBit == -1) rolls the activation odds and fires the
// event at its target spob (LocationSelector - 128, or the first visible spob
// when LocationSelector == -1); linked events instead end (state 2) when their
// bit resolves and the duration is open-ended (< 0).
public static class TickCronTable
{
    public static void Run()
    {
        for (short i = 0; i < CronTable.Count; i++)
        {
            var rec = GameData.Crons[i];

            if (rec.StateCountdown >= 1)
            {
                rec.StateCountdown = (short)(rec.StateCountdown - 1);
                continue;
            }

            if (rec.ControlBit == -1)
            {
                // Idle, unlinked: roll the daily activation odds.
                short roll = (short)SeedEvoRng.Run(100);
                if (rec.DailyOdds < roll + 1)
                {
                    continue;
                }
                // DEAD-CODE NOTE (faithful): the 1000/2000-band gates below test
                // ControlBit, but this branch requires ControlBit == -1 (and
                // LoadCronResources resets any out-of-[0,0x1ff] ControlBit to -1, so a
                // live link is always in [0,511] anyway) — the gates always pass.
                if (rec.LocationSelector < 128)
                {
                    if (rec.LocationSelector == -1)
                    {
                        // Fire at the first visible spob.
                        for (short spob = 0; spob < SpobTable.Count; spob++)
                        {
                            if (GameData.Spobs[spob].Visible == 0)
                            {
                                continue;
                            }
                            if (ActivationGatePasses(rec.ControlBit))
                            {
                                rec.ChosenSpob = spob;
                                rec.StateCountdown = rec.DurationDays;
                            }
                            break;
                        }
                    }
                }
                else if (ActivationGatePasses(rec.ControlBit))
                {
                    // Fire at the resource-designated spob.
                    rec.ChosenSpob = (short)(rec.LocationSelector - 128);
                    rec.StateCountdown = rec.DurationDays;
                }
            }
            else if (rec.ControlBit < 1000)
            {
                // Linked to a control bit: an open-ended event ends when the bit sets.
                if (ControlBits.Get(rec.ControlBit) != 0 &&
                    rec.DurationDays < 0)
                {
                    rec.StateCountdown = 2;
                }
            }
            // DEAD-CODE NOTE (faithful): unreachable — LoadCronResources resets any
            // out-of-[0,0x1ff] ControlBit to -1, so a live (non -1) ControlBit is always
            // in [0,511], which always satisfies the "< 1000" branch above; this
            // "2999 <" branch can never execute.
            else if (2999 < rec.ControlBit &&
                     ControlBits.Get(rec.ControlBit - 3000) == 0 &&
                     rec.DurationDays < 0)
            {
                rec.StateCountdown = 2;
            }
        }
    }

    // The 3-band control-bit gate the decompile inlines at each fire site:
    // 1000..1999 = alias bit must be CLEAR; 2000..2999 = alias bit must be SET;
    // anything else passes.
    private static bool ActivationGatePasses(short link)
    {
        if (999 < link && link < 2000)
        {
            return ControlBits.Get(link - 1000) == 0;
        }
        if (1999 < link && link < 3000)
        {
            return ControlBits.Get(link - 2000) != 0;
        }
        return true;
    }
}
