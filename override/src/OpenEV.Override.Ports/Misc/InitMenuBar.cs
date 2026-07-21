using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 27865-27884.
//
// NO-OP: classic-Mac menu-bar setup (MBAR 128, then append 'DRVR' desk-accessory
// resources to the Apple menu). GetNewMBar is an unwired stub returning 0, so the
// body short-circuits before doing anything — the port draws no menu bar.
public static class InitMenuBar
{
    private const int AppMenuBarId = 128;         // MBAR resource 128
    private const int AppleMenuId = 1000;        // the Apple () menu
    private const int DriverResType = 0x44525652;  // 'DRVR' — desk accessories

    public static void Run()
    {
        int handle = MacToolbox.GetNewMBar(AppMenuBarId);
        if (handle != 0)
        {
            MacToolbox.SetMenuBar(handle);
            handle = MacToolbox.GetMenuHandle(AppleMenuId);
            if (handle != 0)
            {
                MacToolbox.AppendResMenu(handle, DriverResType);
            }
        }
    }
}
