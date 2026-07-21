using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

public static class RestoreMacMenuBar
{
    // Port of FUN_1005eef4 (EV Override-11.c lines 39544-39575).
    public static void Run()
    {
        if (MenuBarState.Hidden != 0)
        {
            int grayRgn = MacToolbox.GetGrayRgn();
            int diffRgn = MacToolbox.NewRgn();
            MacToolbox.DiffRgn(MenuBarState.SavedRgn, grayRgn, diffRgn);
            MacToolbox.CopyRgn(MenuBarState.SavedRgn, grayRgn);
            MacToolbox.LMSetMBarHeight((int)MenuBarState.SavedMBarHeight);
            MenuBarState.Hidden = 0;
            int[] savedPort = new int[4];
            MacToolbox.GetPort(savedPort);
            int frontWindow = MacToolbox.FrontWindow();
            MacToolbox.CalcVisBehind(frontWindow, diffRgn);
            frontWindow = MacToolbox.FrontWindow();
            MacToolbox.PaintBehind(frontWindow, diffRgn);
            MacToolbox.SetPort(savedPort[0]);
            MacToolbox.DisposeRgn(diffRgn);
            MacToolbox.HiliteMenu(0);
            MacToolbox.DrawMenuBar();
        }
    }
}
