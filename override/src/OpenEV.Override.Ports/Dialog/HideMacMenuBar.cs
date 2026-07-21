using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

public static class HideMacMenuBar
{
    // Port of FUN_1005edd8 (EV Override-11.c lines 39507-39543).
    public static void Run()
    {
        if (MenuBarState.Hidden == 0)
        {
            int grayRgn = MacToolbox.GetGrayRgn();
            int diffRgn = MacToolbox.NewRgn();
            MacToolbox.DiffRgn(MenuBarState.GrayRgn, grayRgn, diffRgn);
            MenuBarState.SavedRgn = MacToolbox.NewRgn();
            MacToolbox.CopyRgn(grayRgn, MenuBarState.SavedRgn);
            MacToolbox.UnionRgn(MenuBarState.GrayRgn, grayRgn, grayRgn);
            MenuBarState.SavedMBarHeight = (short)MacToolbox.LMGetMBarHeight();
            MacToolbox.LMSetMBarHeight(0);
            MenuBarState.Hidden = 1;
            int[] savedPort = new int[4];
            MacToolbox.GetPort(savedPort);
            int frontWindow = MacToolbox.FrontWindow();
            MacToolbox.CalcVisBehind(frontWindow, diffRgn);
            frontWindow = MacToolbox.FrontWindow();
            MacToolbox.PaintBehind(frontWindow, diffRgn);
            MacToolbox.SetPort(savedPort[0]);
            MacToolbox.DisposeRgn(diffRgn);
        }
    }
}
