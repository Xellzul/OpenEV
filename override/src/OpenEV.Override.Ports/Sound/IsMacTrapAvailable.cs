namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10075c4c — EV Override-11.c lines 49386-49404.
//
// DEVIATION (faithful): the original idiom probes GetToolboxTrapAddress /
// NGetTrapAddress to test whether trapWord resolves past the _Unimplemented
// stub. The port has no 68k trap table backing those primitives (both are
// UnwiredStubs unconditional-zero), so a faithful port would always report
// "unavailable" and break this function's two original callers
// (IsSoundManagerV3Plus / IsSoundManagerV3_1Plus, probing 0xA800
// _SoundDispatch — present on every machine the game supported). Report true
// unconditionally instead.
// Currently unreferenced: both original callers were ported as independent
// `Run() => true` stubs rather than delegating here. The sibling
// Misc.IsMacTrapAvailable2 (FUN_10078cdc) calls the SAME stubbed
// NGetTrapAddress primitive, so it is equally degraded (always false), not a
// working alternative.
public static class IsMacTrapAvailable
{
    public static bool Run(uint trapWord) => true;
}
