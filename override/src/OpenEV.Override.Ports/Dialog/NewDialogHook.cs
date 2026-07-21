namespace OpenEV.Override.Ports.Dialog;

// FUN_100583c4 (EV Override-11.c lines 36214-36218): `void FUN_100583c4(void) { return; }`.
// Called right after nearly every GetNewDialog in the spaceport/dialog family (25 of
// 30 decompile GetNewDialog call sites) — a stripped debug/positioning hook that is an
// intentional no-op in the shipping game (DDC-07, DEV_DEBUG_CODE.md). The decompile
// dropped the (params) at the declaration, but every call site passes two args (a
// DialogPtr, plus a second value — literal 0 at most sites, a saved-window pointer at
// two of them); the signature matches the call sites, and the body ignores both
// unconditionally, matching the decompile's empty body.
public static class NewDialogHook
{
    public static void Run(int dialogPtr, int unusedArg)
    {
    }
}
