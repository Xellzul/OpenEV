using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Text;

// Port of FUN_1007e4b8 (EV Override-11.c line 54687), called once from InitGalaxyMapWindow.
// Refreshes the eight fatal-alert / error UI strings from STR# 25000, overwriting the compiled-in
// defaults in their data-seg cells (&DAT_1008554c..). The port holds those cells as the managed
// StaticData.UiErrorStrings array, read by the fatal/error alert sites — so this refresh is live.
public static class CopyEightPascalStringBlocks
{
    public static void Run(string[] uiStrings)
    {
        StaticData.UiErrorStrings = (string[])uiStrings.Clone();
    }
}
