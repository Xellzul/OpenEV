using OpenEV.Override.Ports.Core.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Decompile: EV Override-11.c lines 37951-37962.
public static class HideCursorOnce
{
    public static void Run()
    {
        if (!WorldState.IsCursorHiddenByGame)
        {
            WorldState.IsCursorHiddenByGame = true;
            MacToolbox.HideCursor();
        }
        return;
    }
}
