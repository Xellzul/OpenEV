using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Systems;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1005c438 (EV Override-11.c lines 38204-38237). Advances the game
// calendar one day: decrements mission time limits, ticks spob tribute income
// and the cron table, and probabilistically grows each spob's tribute toward
// its cap. Callers loop this N times to fast-forward N days.
public static class TickWorldDailyEvents
{
    public static void Run()
    {
        GameDate.AdvanceCurrentOneDay();

        foreach (var mission in GameData.Missions)
        {
            if (mission.TimeLimit > 0) mission.TimeLimit -= 1;
        }

        TickSpobTributeIncome.Run();
        TickCronTable.Run();

        foreach (var spob in GameData.Spobs)
        {
            if (spob.Visible == 0 || spob.TradingEnabled == 0) continue;

            short threshold = spob.TributeMax < 1001 ? spob.TributeMax : (short)(spob.TributeMax % 1000);
            if (spob.Tribute < threshold && (short)SeedEvoRng.Run(450) == 0)
            {
                spob.Tribute += 1;
            }
        }
    }
}
