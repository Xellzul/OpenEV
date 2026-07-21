using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Boot;

// FUN_10051fbc (EV Override-11.c lines 33573-33597) — toolbox boot init: the
// classic Mac InitGraf/InitFonts/.../MaxApplZone sequence, grow the master-pointer
// block, then seed the RNG.
public static class InitToolboxBootSequence
{
    public static void Run()
    {
        MacToolbox.InitGraf(SystemGlobals.QuickDrawGlobalsPtr + 0xca); // qd.thePort
        MacToolbox.InitFonts();
        MacToolbox.InitWindows();
        MacToolbox.InitMenus();
        MacToolbox.TEInit();
        MacToolbox.InitDialogs(0);
        MacToolbox.InitCursor();
        MacToolbox.FlushEvents(EventMask.EveryEvent, 0);
        MacToolbox.MaxApplZone();
        for (short i = 0; i < 6; i++)
        {   // grow the master-pointer block (fixed 6x per the decompile)
            MacToolbox.MoreMasters();
        }
        SeedEvoRng.Run(0);
    }
}
