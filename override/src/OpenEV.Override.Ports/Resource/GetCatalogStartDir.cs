using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_10061a28 (EV Override-11.c lines 40937-41000): locate a catalog (folder) by alias
// name, returning its dirID + vRefNum. The decompile writes `resFileRefNum` into a stack
// FCBPBRec's ioRefNum field and calls PBGetFCBInfoSync to find that open resource
// file's own volume/dir; it then allocates a second temp record (NewPtrClear), copies
// `aliasName` into it twice, calls ResolveAliasFile, picks either the alias-resolved
// FSSpec or the source FCB's own vRefNum/dirID for the record, then calls the real
// PBGetCatInfoSync and copies its result out before DisposePtr.
//
// DEVIATION (faithful): the port collapses all of that record-building/branch structure —
// the port does NOT build either temp record, does NOT copy `aliasName` anywhere, and
// calls ResolveAliasFile with a hardcoded specPtr of 0 while discarding its outputs.
// This is provably behavior-identical to the decompile TODAY ONLY, because of this
// exact stub chain: MacToolbox.PBGetFCBInfoSync(out,out) hardcodes vRefNum=0/dirID=0
// regardless of input (so `resFileRefNum` is never actually consulted, even though the
// decompile's real trap DOES read it via ioRefNum); MacToolbox.PBGetCatInfoSync(short,
// int,out,out) just echoes its input back (never a real lookup); and
// MacToolbox.ResolveAliasFile's wasAliased is only ever true for a registered
// "Last Pilot" spec token, which specPtr=0 can never be, and neither of this function's
// two decompile call sites (a QuickTime movie-folder lookup, a plug-in folder lookup)
// resolve "Last Pilot" names anyway. Consequence: the `resFileRefNum` and `aliasName`
// parameters below are BOTH dead in the current body purely because of this stub chain
// (kept for FUN-signature parity with the decompile, not currently read) — neither is
// dead code in the original. TODO: restore real record-building + alias resolution here
// if a real file system or alias support is ever added to the File Manager shim —
// re-derive this whole no-op proof first, it depends on all three stub behaviors above
// staying exactly as they are.
public static class GetCatalogStartDir
{
    public static int Run(short resFileRefNum, string aliasName, out int outDirID, out short outVRefNum)
    {
        outDirID = 0;
        outVRefNum = 0;

        int result = MacToolbox.PBGetFCBInfoSync(out short fcbVRefNum, out int fcbDirId);
        if (result != 0)
            return result;

        MacToolbox.ResolveAliasFile(0, 1, out _, out _);

        result = MacToolbox.PBGetCatInfoSync(fcbVRefNum, fcbDirId, out short catVRef, out int catDirId);
        if (result == 0)
        {
            outDirID = catDirId;
            outVRefNum = catVRef;
        }
        return result;
    }
}
