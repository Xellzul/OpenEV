using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_10019880 (EV Override-11.c lines 11592-11787): fill the resource-name
// string caches (boot step 26) — the managed ResourceGlobals.NamesStr* string[]
// tables now (each entry's own dest-slot provenance, including the 0x89/0x8a
// split-TOC gotcha, is documented on the field in ResourceGlobals.cs). Per
// entry a per-id 'STR ' resource wins, else the STR# list entry; the
// 0x138b/0x86/0x88/0x89/0x8a tables are STR#-only.
public static class InitResourceNameStrings
{
    public static void Run()
    {
        FillWithStrFallback(ResourceGlobals.NamesStr4000, 9000, 4000);
        FillWithStrFallback(ResourceGlobals.NamesStr0fa3, 0x24b8, 0xfa3);
        FillWithStrFallback(ResourceGlobals.NamesStr0fa1, 0x238c, 0xfa1);
        FillWithStrFallback(ResourceGlobals.NamesStr5000, 3000, 5000);
        FillWithStrFallback(ResourceGlobals.NamesStr1389, 0xe10, 0x1389);
        FillWithStrFallback(ResourceGlobals.NamesStr138a, 0xe74, 0x138a);
        FillFromStrList(ResourceGlobals.NamesStr138b, 0x138b);
        FillWithStrFallback(ResourceGlobals.NamesStr138c, 0xc80, 0x138c);
        FillWithStrFallback(ResourceGlobals.NamesStr138d, 0xd48, 0x138d);
        FillWithStrFallback(ResourceGlobals.NamesStr0fa5, 8000, 0xfa5);
        FillWithStrFallback(ResourceGlobals.NamesStr0fa6, 0x2008, 0xfa6);
        FillFromStrList(ResourceGlobals.NamesStr0086, 0x86);
        FillFromStrList(ResourceGlobals.NamesStr0088, 0x88);
        FillFromStrList(ResourceGlobals.NamesStr0089, 0x89);
        FillFromStrList(ResourceGlobals.NamesStr008a, 0x8a);
        FillWithStrFallback(ResourceGlobals.NamesStr6000, 4000, 6000);
        FillWithStrFallback(ResourceGlobals.NamesStr0fa2, 0x23f0, 0xfa2);
    }

    // Per-id 'STR ' resource (strResBase + i) wins; STR# entry (i+1) is the fallback.
    private static void FillWithStrFallback(string[] table, int strResBase, short strListId)
    {
        for (short i = 0; i < table.Length; i++)
            table[i] = TryLoadStr.RunString((short)(strResBase + i))
                       ?? MacToolbox.GetIndString(strListId, (short)(i + 1));
    }

    private static void FillFromStrList(string[] table, short strListId)
    {
        for (short i = 0; i < table.Length; i++)
            table[i] = MacToolbox.GetIndString(strListId, (short)(i + 1));
    }
}
