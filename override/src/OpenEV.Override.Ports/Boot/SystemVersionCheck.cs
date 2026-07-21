using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Title;

namespace OpenEV.Override.Ports.Boot;

// FUN_1005466c (EV Override-11.c lines 34513-34548) — boot step 3: the SysEnvirons
// System-7 gate, stamp a startup slot + the QuickTime-present flag, then copy the
// main screen bounds into the game-window bounds-rect record.
public static class SystemVersionCheck
{
    private const int QuickTimeGestaltSelector = 0x7174696d;  // 'qtim'

    public static void Run()
    {
        // SysEnvirons fills SysEnvRec.systemVersion (+4); the MacToolbox shim no-ops, so it
        // never writes the version. The host trivially satisfies the System-7.0 gate, so treat
        // the reported version as 0x700 (passing) — the OS-too-old branch below is dead.
        MacToolbox.SysEnvirons(2, 0);   // 0 = the (ignored) SysEnvRec buffer pointer
        short systemVersion = 0x700;

        if (systemVersion < 0x700)
        {
            TearDownSavedPalette.Run();
            // Pascal string @0x10084883 (GameToc-0x3ddd, dumped).
            AlertModal_OneButton.Run("Sorry, EV requires System 7 or greater.");
            RestoreMacMenuBar.Run();
            TeardownSoundSubsystem.Run();
            MacToolbox.ExitToShell();
        }
        SystemGlobals.StartupMarker = 8;

        short gestaltErr = MacToolbox.Gestalt(QuickTimeGestaltSelector, out int _);
        SystemGlobals.QuickTimePresent = (byte)(gestaltErr == 0 ? 1 : 0);

        // Copy the main screen's bounds Rect (GDevice gdRect, screen GDevice handle slot
        // 0x10081100) into the game-window bounds record (the heap rect *0x100811bc, now
        // GameWindowGlobals.GameWindowBounds).
        MacToolbox.GetMainDeviceBounds(out short top, out short left, out short bottom, out short right);
        var bounds = GameWindowGlobals.GameWindowBounds;
        bounds[0] = top; bounds[1] = left; bounds[2] = bottom; bounds[3] = right;
    }
}
