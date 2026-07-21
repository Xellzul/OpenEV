using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10071568 (EV Override-11.c 46528-46548) — protect+reserve every entry of the
// device colour table so the palette manager can't reallocate them.
public static class LockAllPaletteEntries
{
    public static void Run(int gDevice)
    {
        var savedGDevice = MacToolbox.GetGDevice();
        MacToolbox.SetGDevice(gDevice);
        var maxEntryIndex = (short)MacToolbox.ColorTableEntryCount(MacToolbox.DeviceColorTable(gDevice));
        for (int entryIndex = 0; (short)entryIndex <= maxEntryIndex; entryIndex++)
        {
            // Faithful Mac quirk: ProtectEntry is called twice around ReserveEntry
            // (decompile 46539-46541) — keep the original order.
            MacToolbox.ProtectEntry(entryIndex, 0);
            MacToolbox.ReserveEntry(entryIndex, 0);
            MacToolbox.ProtectEntry(entryIndex, 0);
        }
        MacToolbox.SetGDevice(savedGDevice);
    }
}
