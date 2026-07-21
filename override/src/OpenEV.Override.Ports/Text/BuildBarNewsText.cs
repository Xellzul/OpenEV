using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Text;

// Port of FUN_1000aa44 (EV Override-11.c 5669-5794): build the bar-news
// terminal's two static lines into SpaceportGlobals.BarNewsLineA/B.
//   Line A: a random STR# 8100 entry ("" when the list is empty/missing).
//   Line B: prefer an active cron price-news event for the current spob (or a
//           global -2 event; with no local event a 50% coin picks any active
//           event or none), rendered from 'STR ' 6000+i or STR# 8102 entry
//           i+1 — or, when that text is < 2 chars, composed as "Today's Top
//           News: <cron> has raised/lowered the price of <commodity> on
//           <spob>."; otherwise a random STR# 8101 entry ("No news to report"
//           when empty/missing).
public static class BuildBarNewsText
{
    private static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

    // A STR# resource begins with its entry count as a leading 16-bit short.
    private static short StrListCount(short listId)
    {
        int handle = MacToolbox.GetResource(MacResType.StringList, listId);
        if (handle == 0) return 0;
        MacToolbox.HLock(handle);
        short count = MacToolbox.ReadResourceShort(handle, 0);
        MacToolbox.HUnlock(handle);
        MacToolbox.HPurge(handle);
        MacToolbox.ReleaseResource(handle);
        return count;
    }

    public static void Run()
    {
        short countA = StrListCount(8100);
        if (countA < 1)
        {
            SpaceportGlobals.BarNewsLineA = "";
        }
        else
        {
            SpaceportGlobals.BarNewsLineA =
                MacToolbox.GetIndString(8100, (short)(SeedEvoRng.Run(countA) + 1));
        }
        short countB = StrListCount(8101);

        short eligible = 0;
        short total = 0;
        int pick = -1;
        foreach (var cron in GameData.Crons)
        {
            if (1 < cron.StateCountdown)
            {
                total = (short)(total + 1);
                if (GameData.Ships[0].NavTargetSpob == cron.ChosenSpob || cron.LocationSelector == -2)
                    eligible = (short)(eligible + 1);
            }
        }
        if (0 < total)
        {
            if (eligible < 1)
            {
                if (SeedEvoRng.Run(2) == 0)
                {
                    do
                    {
                        pick = (int)SeedEvoRng.Run(CronTable.Count);
                    } while (GameData.Crons[pick].StateCountdown < 2);
                }
                else
                {
                    pick = -1;
                }
            }
            else
            {
                do
                {
                    pick = (int)SeedEvoRng.Run(CronTable.Count);
                } while (GameData.Crons[pick].StateCountdown < 2 ||
                         (GameData.Crons[pick].LocationSelector != -2 &&
                          GameData.Ships[0].NavTargetSpob != GameData.Crons[pick].ChosenSpob));
            }
        }
        if (pick == -1)
        {
            if (countB < 1)
            {
                SpaceportGlobals.BarNewsLineB = "No news to report";
            }
            else
            {
                SpaceportGlobals.BarNewsLineB =
                    MacToolbox.GetIndString(8101, (short)(SeedEvoRng.Run(countB) + 1));
            }
        }
        else
        {
            var cron = GameData.Crons[pick];
            string custom = TryLoadStr.RunString((short)(pick + 6000))
                            ?? MacToolbox.GetIndString(8102, (short)(pick + 1));
            if (custom.Length < 2)
            {
                if (cron.ChosenSpob != -1)
                {
                    string commodity = Trunc(ResourceGlobals.NamesStr0fa1[cron.Commodity], 63);
                    SpaceportGlobals.BarNewsLineB =
                        custom + "Today’s Top News: " +
                        Trunc(cron.Name, 63) +
                        " has " +
                        (cron.PriceDelta < 1 ? "lowered" : "raised") +
                        " the price of " +
                        commodity +
                        " on " +
                        Trunc(GameData.Spobs[cron.ChosenSpob].Name, 31) +
                        ".";
                }
                // ChosenSpob == -1: the original leaves BarNewsLineB untouched
                // (stale previous news shows) — faithful.
            }
            else
            {
                SpaceportGlobals.BarNewsLineB = custom;
            }
        }
    }
}
