namespace OpenEV.Override.Ports.Graphics;

// Decompile: EV Override-11.c lines 48613-48625.
//
// Original: `if (param_1 != 0) *(int *)(param_1 + 6) = param_2;` — stores a
// callback pointer at +6 of an opaque styled-text/notification-library block,
// NOT into any of the sound UPP cells; the "Sound" name predates the B3 audit.
// Sole caller: FUN_100734f4 (CloseSlideShowWindow), restoring
// the saved value record+0x166 into the block at record+0x15e.
// ORIGINAL QUIRK (verified, B3 — grep of every 0x15e/0x166 use in the binary):
// NOTHING ever writes either record field, so the caller's `!= 0` guard never
// passes and this function is unreachable (same dead-branch family as
// SoundProcs.CompletionListHead). Ported as an honest no-op returning 0 (noErr).
//
// Rule 19 (make-nice, considered + reverted, then completed here): an earlier
// pass moved this file to Graphics/ alone, then reverted because its twin
// CallSndUPP.cs (same caller, same "name predates the B3 audit" note) was
// left behind in Sound/ — moving one twin without the other just trades one
// inconsistency for another. This coordinated pass moves BOTH twins together
// to Graphics/, colocating them with their sole caller
// Graphics/CloseSlideShowWindow.cs instead of the sound subsystem neither
// actually touches.
public static class InstallCallbackPtr
{
    public static int Run(int callbackBlock, int callback) => 0;
}
