// Two no-op FUN_xxx stubs, each still called from a real port but inert because the Mac subsystem
// it drives is genuinely unavailable on the host. Reason for the no-op noted at each.

namespace OpenEV.Override.Ports;

// Full-screen 8-bit SlotGWorld creator (FUN_1006f6d4, line 45470): switches the monitor to 256
// colours via SetDepth and builds an offscreen 8-bit CLUT GWorld. Can't be faithfully ported — the
// true-colour host has no CLUT GWorld or SetDepth (rendering goes to managed RenderTargets), so a
// structural port would fail every GWorld attempt and return -3, whereupon the caller
// (InitFullScreenOffscreenWorld) shows the Monitor-Tool error alert and ExitToShell — the game would
// refuse to boot. Kept a no-op returning 0 (noErr), the success every real Mac produced. Do NOT
// revive real SetDepth / GWorld allocation here.
// NO-OP: Mac CLUT-depth path unavailable on the true-colour host.
// Called by Graphics/InitFullScreenOffscreenWorld.cs (return read as errorId).
public static class CreateFullScreenSlotGWorld { public static int Run(params object?[] _) => default; }

// PBCatSearch-style hunt for the Register helper app by 'APPL' type / 'Areg' creator (FUN_10074930,
// line 48526 — NOT LaunchApplicationByFSSpec itself, which is its caller FUN_10072148). Can't be
// faithful — HFS catalog type/creator metadata doesn't exist on the host filesystem, and the
// game->register launch is already handled by host AppLauncher glue (V2TitleAdapter). Returns
// foundCount = 0 / noErr, the faithful outcome of the scan finding nothing on a host volume. Reached
// only when the direct launch fails (AppLauncher returns fnfErr(-43) because OpenEV.Register.exe isn't
// built) — benign. See DEV_DEBUG_CODE.md (DDC-04).
// NO-OP: HFS catalog search unavailable on host. Called by Misc/LaunchApplicationByFSSpec.cs.
public static class CatSearchForRegisterApp
{
    public static int Run(int volNamePtr, short vRefNum, string targetName, int fileList,
                          int maxFiles, out int foundCount, bool forceFreshSearch, bool searchFlag)
    {
        foundCount = 0;
        return 0;   // noErr
    }
}
