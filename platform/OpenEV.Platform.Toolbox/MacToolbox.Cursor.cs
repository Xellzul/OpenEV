using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

// Cursor Manager. The game has no software-drawn on-screen cursor sprite, so InitCursor/GetCursor/
// SetCursor (MacToolbox.cs) and SetCCursor/GetCCursor (here) drive the HOST's native OS cursor
// through three delegates a host wires (e.g. RegisterGame's SDL loop) — null on hosts that never
// wire one, a safe no-op. GetCCursor follows the GetCIcon/GetPixPat pattern (MacToolbox.ColorIcons.cs):
// decode the resource once via the portable OpenEV.Platform.Imaging.CrsrDecoder, cache it, and hand
// back a synthetic handle the host resolves back to the decoded bitmap + hotspot.
public static partial class MacToolbox
{
    /// Host hook: show the plain system arrow (InitCursor).
    public static Action? HostInitCursor;
    /// Host hook: show a built-in numbered CURS resource (SetCursor, after GetCursor(id) succeeded).
    public static Action<int>? HostSetCursor;
    /// Host hook: show a custom colour cursor previously loaded by GetCCursor (SetCCursor(handle)).
    public static Action<int>? HostSetColorCursor;

    private const int ColorCursorKeyBase = 0x19000000;   // distinct from the cicn/ppat key bands
    private static int _nextColorCursorSlot;
    private static readonly Dictionary<int, int> _colorCursorIdToKey = new();
    private static readonly Dictionary<int, (Rgba8Image Image, int HotX, int HotY)> _colorCursors = new();

    /// GetCCursor — load + decode colour cursor 'crsr' `id`, cache it, and return a handle (0 if
    /// the resource is missing or undecodable). Cached per id (the Mac returns the already-loaded
    /// handle on a repeat call).
    public static int GetCCursor(int id)
    {
        if (_colorCursorIdToKey.TryGetValue(id, out int cached)) return cached;
        int key = 0;
        byte[]? bytes = GetResourceImpl?.Invoke((uint)MacResType.ColorCursor, id);
        if (bytes is not null)
        {
            var img = CrsrDecoder.Decode(bytes, out int hotX, out int hotY, $"crsr {id}");
            if (img is not null)
            {
                key = ColorCursorKeyBase + (_nextColorCursorSlot++ * 4);
                _colorCursors[key] = (img, hotX, hotY);
            }
        }
        _colorCursorIdToKey[id] = key;
        return key;
    }

    /// Resolve a GetCCursor handle to its decoded image + hotspot, for the host to build a native cursor.
    public static (Rgba8Image Image, int HotX, int HotY)? ResolveColorCursor(int handle)
        => _colorCursors.TryGetValue(handle, out var c) ? c : null;

    public static void SetCCursor(int handle) => HostSetColorCursor?.Invoke(handle);
}
