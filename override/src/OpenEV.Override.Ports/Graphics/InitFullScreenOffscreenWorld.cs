using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10054a30 — open the full-screen game offscreen world (depth 8, 640x480) on the main
// screen GDevice, then init the palette. Called from GameBootSequence (FUN_10061bb0).
// The decompile reached the saved main-screen GDevice via two different bases; both now go
// through the managed GetMainDevice() accessor, so this port holds no unmanaged state.
public static class InitFullScreenOffscreenWorld
{
    public static void Run()
    {
        short errorId = (short)CreateFullScreenSlotGWorld.Run(8, 640, 480, 1, MacToolbox.GetMainDevice());   // FUN_1006f6d4
        if (errorId != 0)
        {
            // Dead branch: CreateFullScreenSlotGWorld (FUN_1006f6d4) is a stub returning 0, so errorId is always 0. The
            // original built "<error>\r\r\r\rID = <id>" and showed it via the modal alert.
            string msg = Core.Model.StaticData.MonitorToolOpenError + "\r\r\r\r"
                       + Core.Model.StaticData.IdLabel + errorId;
            TearDownSavedPalette.Run();
            Title.AlertModal_OneButton.Run(msg);
            Dialog.RestoreMacMenuBar.Run();
            Sound.TeardownSoundSubsystem.Run();
            MacToolbox.ExitToShell();
        }
        MacToolbox.SetGDevice(MacToolbox.GetMainDevice());
        Model.Palette.Init();
    }
}
