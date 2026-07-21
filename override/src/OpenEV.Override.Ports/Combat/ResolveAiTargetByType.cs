using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10048b80 (EV Override-11.c 30370-30627) — resolve a mission/AI target spob from a type code:
//   -2  random inhabited spob            -3  random uninhabited spob
//   128..1627  literal spob id (code − 128)   10000..14999  same-govt
//   15000..19999 allied-govt   20000..24999  other-govt   25000..29999  enemy-govt
// Returns the chosen spob index, or selfSpob unchanged when no candidate exists. Every candidate must
// be a visible spob in a visible system whose system is disconnected from self's (AreSystsDisconnected
// true); the per-block Landable / (un)inhabited (Uninhabited flag) / Station requirements vary.
public static class ResolveAiTargetByType
{
    public static int Run(int targetType, int selfSpob)
    {
        var typeCode = (short)targetType;
        var result = selfSpob;
        if (typeCode == -1 || typeCode == -4)
            return result;

        var selfIndex = (short)selfSpob;

        if (typeCode == -2)
        {
            short candidateIndex = 0;
            short scanIndex;
            for (scanIndex = 0; scanIndex < SpobTable.Count; scanIndex++)
            {
                var flags = (SpobFlags)Core.Model.GameData.Spobs[scanIndex].Flags;
                if (scanIndex != selfIndex
                    && Core.Model.GameData.Spobs[scanIndex].Visible != 0
                    && (flags & SpobFlags.Landable) != 0
                    && (flags & SpobFlags.Uninhabited) == 0
                    && AreSystsDisconnected.Run(scanIndex, selfIndex))
                    candidateIndex++;
                if (candidateIndex > 0)
                    break;
            }
            if (candidateIndex < 1)
                return selfSpob;
            SpobFlags rolledFlags;
            do
            {
                do
                {
                    result = (int)SeedEvoRng.Run(SpobTable.Count);
                    candidateIndex = (short)result;
                } while (candidateIndex == selfIndex);
                rolledFlags = (SpobFlags)Core.Model.GameData.Spobs[candidateIndex].Flags;
            } while (Core.Model.GameData.Spobs[candidateIndex].Visible == 0
                     || IsSystVisible.Run(Core.Model.GameData.Spobs[candidateIndex].System) == 0
                     || (rolledFlags & SpobFlags.Landable) == 0
                     || (rolledFlags & SpobFlags.Uninhabited) != 0
                     || (rolledFlags & SpobFlags.Station) != 0
                     // Original quirk (decompile 30411): the reachability re-check uses the stale
                     // count-loop index scanIndex, not the candidate being rolled.
                     || !AreSystsDisconnected.Run(scanIndex, selfIndex));
        }

        if (typeCode == -3)
        {
            short candidateIndex = 0;
            for (short scanIndex = 0; scanIndex < SpobTable.Count; scanIndex++)
            {
                // Original quirk (decompile 30417-30426): the count increments OUTSIDE the filter (so
                // the scan always stops after the first spob), and the AreSystsDisconnected result
                // inside the filter is discarded.
                var flags = (SpobFlags)Core.Model.GameData.Spobs[scanIndex].Flags;
                if (scanIndex != selfIndex && Core.Model.GameData.Spobs[scanIndex].Visible != 0
                    && IsSystVisible.Run(Core.Model.GameData.Spobs[scanIndex].System) != 0
                    && (flags & SpobFlags.Uninhabited) != 0
                    && (flags & SpobFlags.Landable) != 0
                    && (flags & SpobFlags.Station) == 0)
                    AreSystsDisconnected.Run(scanIndex, selfIndex);
                candidateIndex++;
                if (candidateIndex > 0)
                    break;
            }
            if (candidateIndex < 1)
                return selfSpob;
            SpobFlags rolledFlags;
            do
            {
                do
                {
                    result = (int)SeedEvoRng.Run(SpobTable.Count);
                    candidateIndex = (short)result;
                } while (candidateIndex == selfIndex);
                rolledFlags = (SpobFlags)Core.Model.GameData.Spobs[candidateIndex].Flags;
            } while (Core.Model.GameData.Spobs[candidateIndex].Visible == 0
                     || IsSystVisible.Run(Core.Model.GameData.Spobs[candidateIndex].System) == 0
                     || (rolledFlags & SpobFlags.Uninhabited) == 0
                     || (rolledFlags & SpobFlags.Landable) == 0
                     || (rolledFlags & SpobFlags.Station) != 0
                     || !AreSystsDisconnected.Run(candidateIndex, selfIndex));
        }

        if (127 < typeCode && typeCode < 1628)
            result = targetType - 128;

        if (9998 < typeCode && typeCode < 15000)
        {
            // When the target govt has flag 0x800 set, candidates keep their Uninhabited-spob exclusion
            // lifted (see the filter below). 0x800 stays a raw mask — GovtFlags labels it
            // "StartDisabledOrDerelict", which doesn't fit this use, so cast the enum field to
            // ushort here rather than name the bit. Bounds guard: typeCode-10000 can
            // exceed the 128-entry govt table (the original read past it); out-of-range -> flag clear.
            bool allowUninhabited = false;
            if (9999 < typeCode && (uint)(typeCode - 10000) < (uint)GovtTable.Count
                && ((ushort)Core.Model.GameData.Governments[typeCode - 10000].Flags & 0x800) != 0)
                allowUninhabited = true;

            short candidateIndex = 0;
            for (short scanIndex = 0; scanIndex < SpobTable.Count; scanIndex++)
            {
                var flags = (SpobFlags)Core.Model.GameData.Spobs[scanIndex].Flags;
                if (scanIndex != selfIndex && Core.Model.GameData.Spobs[scanIndex].Visible != 0
                    && IsSystVisible.Run(Core.Model.GameData.Spobs[scanIndex].System) != 0
                    && typeCode - 10000 == Core.Model.GameData.Spobs[scanIndex].Govt
                    && ((flags & SpobFlags.Uninhabited) == 0 || allowUninhabited)
                    && (flags & SpobFlags.Landable) != 0
                    && AreSystsDisconnected.Run(scanIndex, selfIndex))
                    candidateIndex++;
                if (candidateIndex > 0)
                    break;
            }
            if (candidateIndex < 1)
                return selfSpob;
            SpobFlags rolledFlags;
            do
            {
                do
                {
                    result = (int)SeedEvoRng.Run(SpobTable.Count);
                    candidateIndex = (short)result;
                } while (candidateIndex == selfIndex);
                rolledFlags = (SpobFlags)Core.Model.GameData.Spobs[candidateIndex].Flags;
            } while (Core.Model.GameData.Spobs[candidateIndex].Visible == 0
                     || IsSystVisible.Run(Core.Model.GameData.Spobs[candidateIndex].System) == 0
                     || typeCode - 10000 != Core.Model.GameData.Spobs[candidateIndex].Govt
                     || ((rolledFlags & SpobFlags.Uninhabited) != 0 && !allowUninhabited)
                     || (rolledFlags & SpobFlags.Landable) == 0
                     || !AreSystsDisconnected.Run(candidateIndex, selfIndex));
        }

        if (14999 < typeCode && typeCode < 20000)
        {
            short candidateIndex = 0;
            for (short scanIndex = 0; scanIndex < SpobTable.Count; scanIndex++)
            {
                bool isMatch = AlliedGovtMatch(typeCode, scanIndex);
                var flags = (SpobFlags)Core.Model.GameData.Spobs[scanIndex].Flags;
                if (scanIndex != selfIndex && Core.Model.GameData.Spobs[scanIndex].Visible != 0
                    && IsSystVisible.Run(Core.Model.GameData.Spobs[scanIndex].System) != 0
                    && isMatch
                    && (flags & SpobFlags.Uninhabited) == 0
                    && (flags & SpobFlags.Landable) != 0
                    && AreSystsDisconnected.Run(scanIndex, selfIndex))
                    candidateIndex++;
                if (candidateIndex > 0)
                    break;
            }
            if (candidateIndex < 1)
                return selfSpob;
            bool rolledMatch;
            SpobFlags rolledFlags;
            do
            {
                result = (int)SeedEvoRng.Run(SpobTable.Count);
                candidateIndex = (short)result;
                rolledMatch = AlliedGovtMatch(typeCode, candidateIndex);
                rolledFlags = (SpobFlags)Core.Model.GameData.Spobs[candidateIndex].Flags;
            } while (candidateIndex == selfIndex
                     || Core.Model.GameData.Spobs[candidateIndex].Visible == 0
                     || IsSystVisible.Run(Core.Model.GameData.Spobs[candidateIndex].System) == 0
                     || !rolledMatch
                     || (rolledFlags & SpobFlags.Uninhabited) != 0
                     || (rolledFlags & SpobFlags.Landable) == 0
                     || !AreSystsDisconnected.Run(candidateIndex, selfIndex));
        }

        if (19999 < typeCode && typeCode < 25000)
        {
            short candidateIndex = 0;
            for (short scanIndex = 0; scanIndex < SpobTable.Count; scanIndex++)
            {
                var flags = (SpobFlags)Core.Model.GameData.Spobs[scanIndex].Flags;
                if (scanIndex != selfIndex && Core.Model.GameData.Spobs[scanIndex].Visible != 0
                    && IsSystVisible.Run(Core.Model.GameData.Spobs[scanIndex].System) != 0
                    && typeCode - 20000 != Core.Model.GameData.Spobs[scanIndex].Govt
                    && (flags & SpobFlags.Uninhabited) == 0
                    && (flags & SpobFlags.Landable) != 0
                    && AreSystsDisconnected.Run(scanIndex, selfIndex))
                    candidateIndex++;
                if (candidateIndex > 0)
                    break;
            }
            if (candidateIndex < 1)
                return selfSpob;
            bool systOk;
            SpobFlags rolledFlags;
            do
            {
                do
                {
                    result = (int)SeedEvoRng.Run(SpobTable.Count);
                    candidateIndex = (short)result;
                } while (Core.Model.GameData.Spobs[candidateIndex].Visible == 0);
                systOk = IsSystVisible.Run(Core.Model.GameData.Spobs[candidateIndex].System) != 0;
                rolledFlags = (SpobFlags)Core.Model.GameData.Spobs[candidateIndex].Flags;
            } while (!systOk
                     || typeCode - 20000 == Core.Model.GameData.Spobs[candidateIndex].Govt
                     || (rolledFlags & SpobFlags.Uninhabited) != 0
                     || (rolledFlags & SpobFlags.Landable) == 0
                     || !AreSystsDisconnected.Run(candidateIndex, selfIndex));
        }

        if (24999 < typeCode && typeCode < 30000)
        {
            short candidateIndex = 0;
            for (short scanIndex = 0; scanIndex < SpobTable.Count; scanIndex++)
            {
                bool isMatch = EnemyGovtMatch(typeCode, scanIndex);
                var flags = (SpobFlags)Core.Model.GameData.Spobs[scanIndex].Flags;
                if (scanIndex != selfIndex && Core.Model.GameData.Spobs[scanIndex].Visible != 0
                    && IsSystVisible.Run(Core.Model.GameData.Spobs[scanIndex].System) != 0
                    && isMatch
                    && (flags & SpobFlags.Uninhabited) == 0
                    && (flags & SpobFlags.Landable) != 0
                    && AreSystsDisconnected.Run(scanIndex, selfIndex))
                    candidateIndex++;
                if (candidateIndex > 0)
                    break;
            }
            result = selfSpob;
            if (candidateIndex > 0)
            {
                bool rolledMatch;
                SpobFlags rolledFlags;
                do
                {
                    result = (int)SeedEvoRng.Run(SpobTable.Count);
                    candidateIndex = (short)result;
                    rolledMatch = EnemyGovtMatch(typeCode, candidateIndex);
                    rolledFlags = (SpobFlags)Core.Model.GameData.Spobs[candidateIndex].Flags;
                } while (candidateIndex == selfIndex
                         || Core.Model.GameData.Spobs[candidateIndex].Visible == 0
                         || IsSystVisible.Run(Core.Model.GameData.Spobs[candidateIndex].System) == 0
                         || !rolledMatch
                         || (rolledFlags & SpobFlags.Uninhabited) != 0
                         || (rolledFlags & SpobFlags.Landable) == 0
                         || !AreSystsDisconnected.Run(candidateIndex, selfIndex));
            }
        }

        return result;
    }

    // 15000-block allied-govt filter (decompile 30486-30499, repeated in the re-roll at 30516-30528).
    // Original quirks preserved: the first term reads the SYST table at the SPOB index (different index
    // spaces; syst +4 = govt), and typeCode−15000 (0..4999) can index past the 128-entry govt table —
    // both bounds-guarded here (the original read adjacent heap).
    private static bool AlliedGovtMatch(short typeCode, short spobIndex)
    {
        short spobGovt = Core.Model.GameData.Spobs[spobIndex].Govt;
        int allied = typeCode - 15000;
        if (spobGovt == -1 || allied == spobGovt)
            return false;
        bool isMatch = spobIndex < SystTable.Count && allied == SystTable.Store[spobIndex].Govt;
        bool alliedInRange = (uint)allied < (uint)GovtTable.Count;
        if (Core.Model.GameData.Governments[spobGovt].Ally != -1 && alliedInRange && Core.Model.GameData.Governments[allied].Ally != -1)
        {
            isMatch = allied == Core.Model.GameData.Governments[spobGovt].Ally || isMatch;
            if (Core.Model.GameData.Governments[allied].Ally == spobGovt)
                isMatch = true;
        }
        return isMatch;
    }

    // 25000-block enemy-govt filter (decompile 30570-30582, repeated 30598-30610). Original copy-paste
    // bug preserved: both inner terms use buggyGovtIndex (typeCode−15000, carried over from the allied
    // block) where enemyGovtIndex (typeCode−25000) was meant; for this block that's 10000..14999, so —
    //   * the first compare reads the REAL govt[spobGovt].Enemy but tests it == 10000..14999, which no
    //     normalized govt id (−1..127) can equal — always false;
    //   * the second indexes govt[buggyGovtIndex].Enemy, past the 128-entry table — the original read
    //     adjacent heap, the managed array is bounds-guarded to no-match.
    // Net: the enemy-govt target type never matches a candidate (the block returns selfSpob).
    private static bool EnemyGovtMatch(short typeCode, short spobIndex)
    {
        short spobGovt = Core.Model.GameData.Spobs[spobIndex].Govt;
        int enemyGovtIndex = typeCode - 25000;
        if (spobGovt == -1 || enemyGovtIndex == spobGovt)
            return false;
        if (Core.Model.GameData.Governments[spobGovt].Enemy == -1
            || (uint)enemyGovtIndex >= (uint)GovtTable.Count
            || Core.Model.GameData.Governments[enemyGovtIndex].Enemy == -1)
            return false;
        int buggyGovtIndex = typeCode - 15000;
        bool buggyInRange = (uint)buggyGovtIndex < (uint)GovtTable.Count;
        bool isMatch = buggyGovtIndex == Core.Model.GameData.Governments[spobGovt].Enemy;
        if (buggyInRange && Core.Model.GameData.Governments[buggyGovtIndex].Enemy == spobGovt)
            isMatch = true;
        return isMatch;
    }
}
