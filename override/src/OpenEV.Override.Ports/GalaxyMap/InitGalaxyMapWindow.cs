using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_1005206c (EV Override-11.c lines 33598-33656)
// Creates the main game NewCWindow, shows it, hides the menu bar, sets up the render
// window (InitRenderWindow) over the screen-bounds rect, positions the galaxy-map rect,
// and refreshes the 8 fatal-alert/error UI strings from STR# 25000 into
// Core.Model.StaticData.UiErrorStrings (via CopyEightPascalStringBlocks).
public static class InitGalaxyMapWindow
{
    private const int WindowTitleStr = 0x100847c8; // &DAT_100847c8 — "Galaxy Map" window-title Pascal string in the data segment
    private const short UiErrorStrList = 25000;
    private const int UiErrorStringCount = 8;

    public static void Run()
    {
        GameWindowGlobals.SetMenuBarHidden(true);

        GameWindowGlobals.GameWindowPtr = MacToolbox.NewCWindow(0, GameWindowGlobals.GameWindowBounds, WindowTitleStr, 0, 2, 0, 1, -1);
        MacToolbox.ShowWindow(GameWindowGlobals.GameWindowPtr);
        HideMacMenuBar.Run();

        short[] bounds = GameWindowGlobals.GameWindowBoundsRect();

        MacToolbox.SetPort(GameWindowGlobals.GameWindowPtr);
        GWorldPort.SetSpriteLoopConfig(1, 1, 0, 32);

        int screenDev = GameWindowGlobals.ScreenGDeviceHandle;
        MacToolbox.HLock(screenDev);
        short[] drawBounds = { bounds[0], bounds[1], bounds[2], (short)(bounds[3] - 144) };
        InitRenderWindow.Run(0, 0, drawBounds, GameWindowGlobals.GameWindowPtr, screenDev, true, false, false, refreshPixMap: false, clampToBounds: true);
        MacToolbox.HUnlock(screenDev);

        GlobalState.PortTop = bounds[0];
        GlobalState.PortLeft = bounds[1];
        GlobalState.PortBottom = bounds[2];
        GlobalState.PortRight = bounds[3];

        GameWindowGlobals.SetGalaxyMapRect((short)(bounds[0] - 75), (short)(bounds[1] - 75),
                                           bounds[2], (short)(bounds[3] - 144));

        var uiStrings = new string[UiErrorStringCount];
        for (short i = 0; i < UiErrorStringCount; i++)
            uiStrings[i] = MacToolbox.GetIndString(UiErrorStrList, (short)(i + 1));
        CopyEightPascalStringBlocks.Run(uiStrings);

        GWorldPort.SetCurrentGameWindow(GameWindowGlobals.CurrentWindowSource);
    }
}
