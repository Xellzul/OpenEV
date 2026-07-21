using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Title;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 40088-40101.
public static class FatalGraphicsResourceExit
{
    public static void Run()
    {
        MacToolbox.ShowCursor();
        Palette.FadeOut(4);
        // The decompile computes the alert text pointer from a TOC-relative offset;
        // the real string lives at GameToc-0x3b62 = 0x10084afe (dumped Pascal string;
        // the ’ apostrophe is MacRoman 0xD5).
        AlertModal_OneButton.Run(
            "EV encountered an error while starting up, probably due to lack of memory. " +
            "Please increase EV’s memory allocation and try again.");
        RestoreMacMenuBar.Run();
        TeardownSoundSubsystem.Run();
        TearDownSavedPalette.Run();
        MacToolbox.ExitToShell();
    }
}
