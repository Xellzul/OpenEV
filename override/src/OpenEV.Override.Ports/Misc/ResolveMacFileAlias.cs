using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_1001e940 (EV Override-11.c 13670-13731) — resolve a Mac file alias by name and
// return its folder FSSpec (vRefNum + dirID). The decompile stages `resFileRefNum` into
// the ioRefNum field of a stack FCBPBRec and calls PBGetFCBInfoSync to find that open
// resource file's own volume/dir; it then allocates a temp record (NewPtrClear), BlockMoves
// `name` into it twice, calls ResolveAliasFile, picks either the alias-resolved FSSpec or
// the source FCB's own vRefNum/dirID for the record, then calls PBGetCatInfoSync and copies
// its result out before DisposePtr.
//
// DEVIATION (faithful): the port collapses all of that record-building/branch structure —
// it does NOT stage `resFileRefNum` anywhere, does NOT build the temp record, does NOT copy
// `name` anywhere, and calls ResolveAliasFile with a hardcoded specPtr of 0 while discarding
// its outputs. Provably behavior-identical TODAY ONLY, via this exact stub chain:
// PBGetFCBInfoSync(out,out) takes no input at all and hardcodes vRefNum=0/dirID=0 regardless
// of the real ioRefNum it would have read; PBGetCatInfoSync(short,int,out,out) just echoes
// its input back (never a real lookup); ResolveAliasFile's wasAliased is only ever true for
// a registered "Last Pilot" spec token, which specPtr=0 can never be. Since both decompile
// branches (alias-resolved vs. source-fallback) write the SAME vRefNum/dirID into the record
// — they only differ in the NAME field, which the stubbed PBGetCatInfoSync never reads — the
// branch is genuinely irrelevant to the output here. Consequence: `resFileRefNum` and `name`
// are BOTH dead in the current body purely because of this stub chain (kept for FUN-signature
// parity with the decompile, not currently read) — neither is dead code in the original
// (ioRefNum is a real PBGetFCBInfoSync input selecting which file's FCB to query). TODO:
// restore real record-building + alias resolution here if a real file system or alias
// support is ever added — re-derive this whole no-op proof first, it depends on all three
// stub behaviors above staying exactly as they are.
public static class ResolveMacFileAlias
{
    public static int Run(short resFileRefNum, string name, out int dirId, out short outVRefNum)
    {
        dirId = 0;
        outVRefNum = 0;

        // FCB info for the current resource file (its volume + parent dir).
        int result = MacToolbox.PBGetFCBInfoSync(out short fcbVRefNum, out int fcbDirId);
        if (result != 0)
            return result;

        MacToolbox.ResolveAliasFile(0, 1, out _, out _);

        // Catalog info for that folder FSSpec (no file system in the port → passes through).
        result = MacToolbox.PBGetCatInfoSync(fcbVRefNum, fcbDirId, out short folderVRefNum, out int folderDirId);
        if (result == 0)
        {
            outVRefNum = folderVRefNum;
            dirId = folderDirId;
        }
        return result;
    }
}
