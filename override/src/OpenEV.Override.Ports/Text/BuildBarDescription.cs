using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Text;

// FUN_1001291c (EV Override-11.c 9371-9538) — builds the spaceport bar-person's
// greeting into DialogScratch.SpaceportGreetText: the govt/pers STR greet (or its
// STR# fallback), optionally REPLACED by a commodity-price rumour, a dude-class
// STR# pick, or the pers mission-brief STR.
public static class BuildBarDescription
{
    // Price-rumour text fragments assembled in the barType == 1 branch.
    private const string RumourPrefix = "The last time I was on ";
    private const string RumourPriceOf = ", the price of ";
    private const string RumourReally = " was really ";
    private const string RumourPretty = " was pretty ";
    private const string RumourVery = " was very ";
    private const string RumourLow = "low.";
    private const string RumourHigh = "high.";

    public static void Run()
    {
        var person = ShipTable.FromPtr(DialogScratch.DialogShipPtr);

        if (person.AiBehaviorType < ShipAiType.Warship)
        {
            DialogScratch.SpaceportGreetText =
                TryLoadStr.RunString((short)(person.Govt * 10 + DialogScratch.SpaceportGreetIndex + 10010))
                ?? MacToolbox.GetIndString((short)(person.Govt + 7000), (short)(DialogScratch.SpaceportGreetIndex + 1));
        }
        else
        {
            DialogScratch.SpaceportGreetText =
                TryLoadStr.RunString((short)(person.Govt * 10 + DialogScratch.SpaceportGreetIndex + 10015))
                ?? MacToolbox.GetIndString((short)(person.Govt + 7000), (short)(DialogScratch.SpaceportGreetIndex + 6));
        }

        // A '*' at the Pascal length byte or first char, or a too-short greet, falls back
        // to the generic STR# 3000 line. The decompile's length test is strlen over the
        // shared Pascal buffer (length byte + content), so its "< 3" maps to this managed
        // "< 2" whenever that buffer has no stale non-zero tail from a longer prior fill —
        // TryLoadStr/GetIndString decode to a clean string here (an already-disclosed
        // deviation, see TryLoadStr.cs), so that original raw-buffer edge case can't recur.
        string greet = DialogScratch.SpaceportGreetText;
        if (greet.Length == '*' || greet.StartsWith("*") || greet.Length < 2)
        {
            DialogScratch.SpaceportGreetText =
                MacToolbox.GetIndString(3000, (short)(DialogScratch.SpaceportGreetIndex + 46));
        }

        short barType = -1;
        if (person.DudeSpawnIndex != -1 &&
            999 < GameData.DudeSpawns[person.DudeSpawnIndex].BarPattern &&
            GameData.DudeSpawns[person.DudeSpawnIndex].BarPattern < 7128)
        {
            ushort govtFlags = (ushort)(GameData.DudeSpawns[person.DudeSpawnIndex].BarPattern / 1000);
            bool matched;
            do
            {
                matched = false;
                barType = (short)SeedEvoRng.Run(3);
                if (barType == 0 && (govtFlags == 1 || govtFlags == 3 || govtFlags == 5 || govtFlags == 7))
                {
                    matched = true;
                }
                if (barType == 1 && (govtFlags == 2 || govtFlags == 3 || govtFlags == 6 || govtFlags == 7))
                {
                    matched = true;
                }
                // Value-set equality, NOT a bit test: 4/6/7 excludes 5, so this must not be
                // "simplified" to (govtFlags & 4) != 0.
                if (barType == 2 && (govtFlags == 4 || govtFlags == 6 || govtFlags == 7))
                {
                    matched = true;
                }
            } while (!matched);
        }

        if (person.PersIndex != -1 &&
            ((PersFlags)GameData.Pers[person.PersIndex].Flags & PersFlags.DisasterInfo) != 0 &&
            GameData.Pers[person.PersIndex].CommQuote == -1)
        {
            barType = 1;
        }

        if (barType == 0)
        {
            short unusedTradingSpobCount = 0;
            for (short spin = 0; spin < SpobTable.Count; spin++)
            {
                if (GameData.Spobs[spin].Visible != '\0' &&
                    ((SpobFlags)GameData.Spobs[spin].Flags & (SpobFlags.Landable | SpobFlags.Exchange)) != 0)
                {
                    unusedTradingSpobCount++;   // ASM r30 — original dead store, never read (register reused ~80 instructions later for the barType==1 cron count)
                }
            }
            short spob;
            do
            {
                int randVal = (int)SeedEvoRng.Run(6);
                spob = (short)SeedEvoRng.Run(SpobTable.Count);
                if (GameData.Spobs[spob].Visible == '\0' ||
                    ((SpobFlags)GameData.Spobs[spob].Flags & (SpobFlags.Landable | SpobFlags.Exchange)) == 0)
                {
                    spob = -1;
                }
                else
                {
                    short mode = (short)CommodityPriceMode.Run((short)randVal, (uint)GameData.Spobs[spob].Flags);
                    if (mode != 1 && mode != 4)
                    {
                        spob = -1;
                    }
                }
            } while (spob == -1);
        }

        if (barType == 1)
        {
            short count = 0;
            foreach (var cronEntry in GameData.Crons)
            {
                if (1 < cronEntry.StateCountdown)
                {
                    count = (short)(count + 1);
                }
            }
            short cron = -1;
            if (count == 1)
            {
                cron = 0;
                while (cron < CronTable.Count && GameData.Crons[cron].StateCountdown < 2)
                {
                    cron = (short)(cron + 1);
                }
            }
            else if (1 < count)
            {
                do
                {
                    cron = (short)SeedEvoRng.Run(CronTable.Count);
                } while (GameData.Crons[cron].StateCountdown < 2);
            }
            if (cron != -1 && -1 < GameData.Crons[cron].ChosenSpob &&
                GameData.Crons[cron].ChosenSpob < SpobTable.Count)
            {
                string spobName = GameData.Spobs[GameData.Crons[cron].ChosenSpob].Name;
                if (spobName.Length > 29)
                {
                    spobName = spobName.Substring(0, 29);
                }
                string commodity = ResourceGlobals.NamesStr0fa1[GameData.Crons[cron].Commodity];
                string desc = RumourPrefix + spobName + RumourPriceOf + commodity;
                short which = (short)SeedEvoRng.Run(3);
                if (which == 0)
                {
                    desc += RumourReally;
                }
                else if (which == 1)
                {
                    desc += RumourPretty;
                }
                else
                {
                    desc += RumourVery;
                }
                desc += GameData.Crons[cron].PriceDelta < 0 ? RumourLow : RumourHigh;
                DialogScratch.SpaceportGreetText = TextScratch.Trunc(desc, 255);
            }
        }

        if (barType == 2)
        {
            short strCount = 0;
            int strHandle = MacToolbox.GetResource(MacResType.StringList,
                (int)((uint)GameData.DudeSpawns[person.DudeSpawnIndex].BarPattern % 1000 + 7500));
            if (strHandle != 0)
            {
                MacToolbox.HLock(strHandle);
                strCount = MacToolbox.ReadResourceShort(strHandle, 0); // STR# entry-count word
                MacToolbox.HUnlock(strHandle);
                MacToolbox.HPurge(strHandle);
                MacToolbox.ReleaseResource(strHandle);
            }
            if (0 < strCount)
            {
                int randPick = (int)SeedEvoRng.Run((short)((DialogScratch.SpaceportSelCellA -
                    DialogScratch.SpaceportSelCellA / strCount * strCount) + 1));
                string pick = MacToolbox.GetIndString(
                    (short)((uint)GameData.DudeSpawns[person.DudeSpawnIndex].BarPattern % 1000 + 7500),
                    (short)(randPick + 1));
                DialogScratch.SpaceportGreetText = TextScratch.Trunc(pick, 255);
            }
        }

        if (person.PersIndex != -1 && 0 < GameData.Pers[person.PersIndex].CommQuote)
        {
            DialogScratch.SpaceportGreetText =
                TryLoadStr.RunString((short)(GameData.Pers[person.PersIndex].CommQuote + 12000))
                ?? MacToolbox.GetIndString(7100, GameData.Pers[person.PersIndex].CommQuote);
        }
    }
}
