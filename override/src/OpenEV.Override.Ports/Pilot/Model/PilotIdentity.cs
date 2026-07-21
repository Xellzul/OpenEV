namespace OpenEV.Override.Ports.Pilot.Model;

// Per-pilot identity/file globals — all managed now.
//   0x1009020c (DAT_1009020c) — the current pilot NAME, a fixed Str255 buffer (an address
//     passed to FSMakeFSSpec / read as a Pascal string; not a pointer slot).
//   0x100870de — the open pilot file's resource refNum (short; set after FSpOpenResFile,
//     used by UpdateResFile/CloseResFile). Sits just past the plugin refNum array
//     (PluginResourceRefs, 0x100870d0..da).
public static class PilotIdentity
{
    // The pilot name and player ship name, now managed C# strings. Their old fixed
    // Str255 buffers (0x1009020c = toc+0x7bac and 0x1009040c = toc+0x7dac) are retired
    // (OriginalGameStateTotalBytes).
    public static string Name = "";
    public static string ShipName = "";

    // The name-entry CAPTURE: whatever the player typed in the last christen /
    // new-pilot text dialog. Was the buffer behind the *(toc-0x799c) pointer cell
    // (= *(0x10080cc4) under GameToc — a split-TOC pair the managed string unifies).
    public static string CapturedNameEntry = "";

    // The open pilot file's resource refNum — managed short (the GameToc-0x1582
    // readers in Save/Load all route here).
    public static short FileRefNum;
}
