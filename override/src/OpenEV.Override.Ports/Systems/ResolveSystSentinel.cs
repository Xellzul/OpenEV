using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_10049c5c (EV Override-11.c lines 30628-30799).
public static class ResolveSystSentinel
{
    public static int Run(int sentinel, int fallback)
    {
        short code = (short)sentinel;
        int reachable = sentinel;
        int chosenSyst = fallback;

        if (code == -2)
        {
            short matchCount = 0;
            for (short systIndex = 0; systIndex < SystTable.Count; systIndex = (short)(systIndex + 1))
            {
                if (SystTable.Store[systIndex].ShownFlag != 0)
                {
                    matchCount = (short)(matchCount + 1);
                }
                if (0 < matchCount) break;
            }
            if (matchCount < 1)
            {
                return sentinel;
            }
            do
            {
                do
                {
                    chosenSyst = (int)SeedEvoRng.Run(SystTable.Count);
                } while (SystTable.Store[(short)chosenSyst].ShownFlag == 0);
                reachable = IsSystVisible.Run((short)chosenSyst);
            } while ((reachable & 0xff) == 0);
        }

        if (code == -5)
        {
            short hyperlinkSlot;
            short systIndex;
            do
            {
                do
                {
                    hyperlinkSlot = (short)SeedEvoRng.Run(SystRecord.HyperLinkCount);
                    systIndex = (short)fallback;
                } while (SystTable.Store[systIndex].HyperLink[hyperlinkSlot] == -1);
            } while (SystTable.Store[SystTable.Store[systIndex].HyperLink[hyperlinkSlot]].ShownFlag == 0 ||
                    ((reachable = IsSystVisible.Run(SystTable.Store[systIndex].HyperLink[hyperlinkSlot])) & 0xff) == 0);
            chosenSyst = SystTable.Store[systIndex].HyperLink[hyperlinkSlot];
        }

        if (127 < code && code < 1128)
        {
            chosenSyst = sentinel - 128;
        }

        if (9998 < code && code < 15000)
        {
            short matchCount = 0;
            short systIndex;
            for (int loopIndex = 0; (systIndex = (short)loopIndex) < SystTable.Count; loopIndex = loopIndex + 1)
            {
                if (SystTable.Store[systIndex].ShownFlag != 0 &&
                    ((reachable = IsSystVisible.Run((short)loopIndex)) & 0xff) != 0 &&
                    code + -10000 == SystTable.Store[systIndex].Govt)
                {
                    matchCount = (short)(matchCount + 1);
                }
                if (0 < matchCount) break;
            }
            if (matchCount < 1)
            {
                return reachable;
            }
            do
            {
                do
                {
                    chosenSyst = (int)SeedEvoRng.Run(SystTable.Count);
                } while (SystTable.Store[(short)chosenSyst].ShownFlag == 0);
                reachable = IsSystVisible.Run((short)chosenSyst);
            } while ((reachable & 0xff) == 0 ||
                    code + -10000 != SystTable.Store[(short)chosenSyst].Govt);
        }

        if (14999 < code && code < 20000)
        {
            short matchCount = 0;
            short systIndex;
            for (int loopIndex = 0; (systIndex = (short)loopIndex) < SystTable.Count; loopIndex = loopIndex + 1)
            {
                short govt = SystTable.Store[systIndex].Govt;
                bool allyMatch = govt >= 0 && (code - 15000) != govt && GovtIsMutualAlly(govt, code - 15000);
                if (SystTable.Store[systIndex].ShownFlag != 0 &&
                    ((reachable = IsSystVisible.Run((short)loopIndex)) & 0xff) != 0 && allyMatch)
                {
                    matchCount = (short)(matchCount + 1);
                }
                if (0 < matchCount) break;
            }
            if (matchCount < 1)
            {
                return reachable;
            }
            short retryGovt;
            bool retryAllyMatch;
            do
            {
                chosenSyst = (int)SeedEvoRng.Run(SystTable.Count);
                retryGovt = SystTable.Store[(short)chosenSyst].Govt;
                retryAllyMatch = retryGovt >= 0 && (code - 15000) != retryGovt && GovtIsMutualAlly(retryGovt, code - 15000);
            } while (SystTable.Store[(short)chosenSyst].ShownFlag == 0 ||
                    ((reachable = IsSystVisible.Run((short)chosenSyst)) & 0xff) == 0 ||
                    !retryAllyMatch);
        }

        if (19999 < code && code < 25000)
        {
            short matchCount = 0;
            short systIndex;
            for (int loopIndex = 0; (systIndex = (short)loopIndex) < SystTable.Count; loopIndex = loopIndex + 1)
            {
                if (SystTable.Store[systIndex].ShownFlag != 0 &&
                    ((reachable = IsSystVisible.Run((short)loopIndex)) & 0xff) != 0 &&
                    code + -20000 != SystTable.Store[systIndex].Govt)
                {
                    matchCount = (short)(matchCount + 1);
                }
                if (0 < matchCount) break;
            }
            if (matchCount < 1)
            {
                return reachable;
            }
            do
            {
                do
                {
                    chosenSyst = (int)SeedEvoRng.Run(SystTable.Count);
                } while (SystTable.Store[(short)chosenSyst].ShownFlag == 0);
                reachable = IsSystVisible.Run((short)chosenSyst);
            } while ((reachable & 0xff) == 0 ||
                    code + -20000 == SystTable.Store[(short)chosenSyst].Govt);
        }

        if (24999 < code && code < 30000)
        {
            short matchCount = 0;
            short systIndex;
            for (int loopIndex = 0; (systIndex = (short)loopIndex) < SystTable.Count; loopIndex = loopIndex + 1)
            {
                short govt = SystTable.Store[systIndex].Govt;
                // ORIGINAL BUG (kept, bug-for-bug parity): wantedGovt below is (code - 15000),
                // not (code - 25000) — see GovtIsMutualEnemy for detail.
                bool enemyMatch = govt >= 0 && (code - 25000) != govt && GovtIsMutualEnemy(govt, code - 15000);
                if (SystTable.Store[systIndex].ShownFlag != 0 &&
                    ((reachable = IsSystVisible.Run((short)loopIndex)) & 0xff) != 0 && enemyMatch)
                {
                    matchCount = (short)(matchCount + 1);
                }
                if (0 < matchCount) break;
            }
            // No early return here (unlike the sibling govt branches above) — this is the
            // decompile's own structure: it falls through with chosenSyst = reachable and
            // only retries below when a match was found.
            chosenSyst = reachable;
            if (0 < matchCount)
            {
                short retryGovt;
                bool retryEnemyMatch;
                byte reachableFlag;
                do
                {
                    chosenSyst = (int)SeedEvoRng.Run(SystTable.Count);
                    retryGovt = SystTable.Store[(short)chosenSyst].Govt;
                    retryEnemyMatch = retryGovt >= 0 && (code - 25000) != retryGovt && GovtIsMutualEnemy(retryGovt, code - 15000);
                } while (SystTable.Store[(short)chosenSyst].ShownFlag == 0 ||
                        (reachableFlag = (byte)IsSystVisible.Run((short)chosenSyst)) == 0 ||
                        !retryEnemyMatch);
            }
        }

        return chosenSyst;
    }

    // FUN_10049c5c 49F3C-49FF4 (loop) / 4A058-4A120 (retry) — "Ally" mutual-relation test:
    // true if wantedGovt is govt's Ally, or govt is wantedGovt's Ally.
    // AtOrPastTable: wantedGovt comes from a resource sentinel band and can index past the
    // 128-entry govt table; the original reads heap garbage there without crashing (see GovtTable).
    private static bool GovtIsMutualAlly(short govt, int wantedGovt) =>
        wantedGovt == GameData.Governments[govt].Ally ||
        GovtTable.AtOrPastTable(wantedGovt).Ally == govt;

    // FUN_10049c5c 4A28C-4A344 (loop) / 4A3A8-4A470 (retry) — "Enemy" mutual-relation test.
    // ORIGINAL BUG (kept, bug-for-bug parity): both lookups here use wantedGovt = code-15000,
    // not code-25000 (this branch's own base) — confirmed in EV Override-11.c 30763/30784 and
    // the ASM (`addi r7,r4,-0x3A98` at loc_4A28C, offset 0x3A98 = 15000). Callers pass
    // code-15000 to preserve this. A real 25000-30000 sentinel therefore always lands
    // wantedGovt (code-15000 >= 10000) past the table → AtOrPastTable's no-match stand-in.
    private static bool GovtIsMutualEnemy(short govt, int wantedGovt) =>
        wantedGovt == GameData.Governments[govt].Enemy ||
        GovtTable.AtOrPastTable(wantedGovt).Enemy == govt;
}
