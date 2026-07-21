using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Systems.Model;

// The "current spaceport stellar (spöb) record" the player is landed on /
// interacting with. The original kept a POINTER into the spob heap in
// 0x1008a504 (`iRam1008a504`, toc+0x1ea4), set on entry to the spaceport hub
// (FUN_10036e74) and dereferenced everywhere (`*(uint*)(iRam1008a504+0x1a)` =
// service flags, +0x20 = free-refuel byte, +6 = a short...). The spob heap is
// now the typed SpobRecord Store, and the ptr cell has no raw backing — the
// managed canonical is the INDEX below.
public static class CurrentSpob
{
    public static int Index;

    public static SpobRecord Rec => GameData.Spobs[Index];

    // Legacy record ADDRESS for callers that still construct a SpobRec by
    // pointer (e.g. RunShipyardDialog -> new SpobRec(CurrentSpob.Base) ->
    // BuildAvailableShipList). Address arithmetic only; no byte is read.
    public static int Base => SpobTable.Base + Index * SpobTable.Stride;
}
