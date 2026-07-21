using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// Port of FUN_10019eec (EV Override-11.c line 11788).
// DEVIATION (faithful): the ASM's BlockMoveData address form copies the
// resource into a caller-supplied buffer and returns bool; every one of
// RunString's 9 callers wants managed text, so it returns the decoded
// Pascal string directly (null when the resource is absent, callers fall
// back to GetIndString) instead of writing through a destAddr.
public static class TryLoadStr
{
    public static string? RunString(short strId)
    {
        int handle = MacToolbox.GetResource(MacResType.String, strId);
        if (handle == 0) return null;
        MacToolbox.HNoPurge(handle);
        // 'STR ' is a Pascal string (length byte + Mac-Roman body). Decode the body as Mac-Roman
        // (PascalToString → MacRomanToString), NOT (char)b — a Latin-1 cast that mangled the high
        // bytes (curly apostrophe 0xD5 → U+00D5 'Õ'); this also matches GetIndString, the fallback
        // these callers use, so the two paths no longer disagree on non-ASCII text.
        byte[]? bytes = MacToolbox.ResourceBytes(handle);
        MacToolbox.HPurge(handle);
        MacToolbox.ReleaseResource(handle);
        return bytes is null ? "" : MacToolbox.PascalToString(bytes);
    }
}
