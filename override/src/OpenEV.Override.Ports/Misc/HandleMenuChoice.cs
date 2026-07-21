using OpenEV.Override.Ports.Title;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_100442ac (EV Override-11.c lines 28303-28329).
public static class HandleMenuChoice
{
    private const int AppleMenuId = 1000;   // MENU 1000: About + desk accessories
    private const int QuitMenuId = 128;     // MENU 128 (item 1 = Quit) — NOT the Apple menu (that's
                                             // 1000; 128 only coincides with MBAR 128/AppMenuBarId)

    public static void Run(int menuSelection)
    {
        short menuId = (short)((uint)menuSelection >> 16);  // high word = menu ID
        if (menuId == AppleMenuId)
        {
            if ((short)menuSelection == 1)
            {
                AboutEvoModal.Run();
            }
            else
            {
                // daNameBuf is a 268-byte Pascal-string scratch address in the original (desk-accessory
                // name buffer), passed to GetMenuItemText and OpenDeskAcc. Both are no-op Toolbox stubs
                // (never write/read it) — GetMenuHandle and HiliteMenu below are likewise unwired stubs —
                // so no real backing buffer is needed; benign at runtime.
                int daNameBuf = 0;
                MacToolbox.GetMenuItemText(MacToolbox.GetMenuHandle(AppleMenuId), menuSelection, daNameBuf);
                MacToolbox.OpenDeskAcc(daNameBuf);
            }
        }
        else if (menuId < AppleMenuId && menuId == QuitMenuId && (short)menuSelection == 1)
        {
            EvoGlobals.QuitRequested = true;
        }
        MacToolbox.HiliteMenu(0);
        return;
    }
}
