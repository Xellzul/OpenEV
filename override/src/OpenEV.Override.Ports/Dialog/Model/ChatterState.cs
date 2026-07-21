namespace OpenEV.Override.Ports.Dialog.Model;

// Managed home for the HUD chatter-line replay state. The original kept the
// last enqueued message in a BSS buffer behind the PEF-relocated pointer cell
// 0x1008122c (EnqueueChatterEvent strncpy'd every message into it, 0xff cap;
// DispatchPendingChatter re-rendered it while the flash countdown ran). The
// message lives here as a real C# string.
public static class ChatterState
{
    public static string LastMessage = "";
}
