using OpenEV.Override.Ports.Core.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_1005232c (EV Override-11.c 33688-33777): NewPtr'd the 21 game tables, published each base
// to its TOC data-seg slot, then — gated by bug-bit 4 (BugBit.DebugStackSpaceDump) — DebugStr'd
// the remaining stack space. The whole EvoMemory-removal campaign moved every one of those tables
// + scalars into typed managed homes (Galaxy.*Table.Store, Core.Model.WorldState / GameData /
// GalaxyMapState / ShipyardState, Dialog.* globals, Misc.ShareWareGlobals, …); EvoMemory itself
// (and the 21 NewPtr table allocs / base publishes it backed) is gone. The debug-bit-gated
// diagnostic tail IS live body content, though — see the bottom of Run below.
//
// GameData.AlertDialog and the WorldState.Spawn*Default fields below are NOT FUN_1005232c body
// content — the decompile never writes them here. AlertDialog's boot value is just the natural Mac
// BSS zero-init (its few explicit "= 0" writes in the decompile are inside unrelated functions —
// FUN_1003e0c8 and FUN_1003e23c; every RunXxxDialog/AlertModal_* site also resets it before
// allocating its own dialog). The five Spawn* fields are read-only PEF data-segment float
// constants (_DAT_10082138..48, verified big-endian vs the decompressed PEF data segment) with
// no writer anywhere in the original.
// Both get seeded here because this boot slot runs once, before any reader (GameBootSequence runs
// this ahead of ResetWorldStateForNewPilot, the Spawn* fields' only other reader).
public static class OriginalGameStateTotalBytes
{
    // The boot registry's managed "game tables allocated" signal (replaced the 21 raw
    // EvoMemory.WriteInt(tocBase+off, base) publishes in G9f). V2TitleAdapter's universe-loaded
    // gates read it; the records themselves live in the managed Galaxy.*Table.Store.
    public static bool GameTablesAllocated { get; private set; }

    public static void Run()
    {
        GameData.AlertDialog = 0;

        WorldState.SpawnField1cDefault = -999.0f;  // _DAT_10082138
        WorldState.SpawnPosDefault = 50.0f;        // _DAT_1008213c
        WorldState.SpawnField20Default = 1.0f;     // _DAT_10082140
        WorldState.SpawnVelDefault = 0.0f;         // _DAT_10082144
        WorldState.SpawnFuelDefault = 100.0f;      // _DAT_10082148

        // Decompile 33760-33770: bug-bit-4-gated DebugStr of the remaining stack space, after the
        // (now-removed) 21 array allocations. DebugStr/StackSpace are unconditional no-op Toolbox
        // stubs (no attached low-level debugger in the port) — inert today, same idiom as
        // LoadSpriteSheetsAndGWorlds' identical bit-4 diagnostic. Kept faithful; see DEV_DEBUG_CODE.md.
        if (BugBits.IsSet(BugBit.DebugStackSpaceDump))
        {
            MacToolbox.DebugStr($"After creating arrays, stackspace is {MacToolbox.StackSpace()} bytes");
        }

        GameTablesAllocated = true;
    }
}
