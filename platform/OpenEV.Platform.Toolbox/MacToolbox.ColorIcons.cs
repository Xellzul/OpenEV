using System.Collections.Generic;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

// Colour-icon ('cicn') support. The Mac Resource Manager loads a 'cicn' as a
// CIcon record (PixMap + 1-bit mask + bitmap + colour table + pixel data);
// GetCIcon/PlotCIcon plot it masked. The game decodes the resource via the portable
// OpenEV.Platform.Imaging.CicnDecoder (folding the mask into alpha) and registers the
// result as a scratch pixmap, so BOTH consumption paths resolve it the same way:
//   • direct  — PlotCIcon blits it into the current port (galaxy-map location/
//               target icons cicn 15000/15001).
//   • sprite  — LoadCIconToSprite (Ports) wraps the handle in a sprite-frame
//               record (ColorRef = this key) so the node CopyMask blits it
//               (target brackets cicn 0x2718.., hyperspace star-streaks cicn
//               1000.., docking rings, HUD blink-orb).
public static partial class MacToolbox
{
    // cicn pixmap-key band — distinct from ship (0x13000000) / planet (0x15000000)
    // / title-orb (0x1A000000) sprite keys.
    private const int CIconKeyBase = 0x16000000;
    private static int _nextCIconSlot;
    private static readonly Dictionary<int, int> _ciconIdToKey = new();   // cicn id → scratch-pixmap key

    /// GetCIcon — load + decode colour icon 'cicn' `id` into an Rgba8Image,
    /// register it as a scratch pixmap, and return its key as the icon handle
    /// (0 if the resource is missing or undecodable). Cached per id (the Mac
    /// returns the already-loaded handle on a repeat call).
    public static int GetCIcon(int id)
    {
        if (_ciconIdToKey.TryGetValue(id, out int cached)) return cached;
        int key = 0;
        byte[]? bytes = GetResourceImpl?.Invoke((uint)MacResType.ColorIcon, id);
        if (bytes is not null)
        {
            var img = CicnDecoder.Decode(bytes, $"cicn {id}");
            if (img is not null)
            {
                key = CIconKeyBase + (_nextCIconSlot++ * 4);
                SetScratchPixmap(key, img);
            }
        }
        _ciconIdToKey[id] = key;
        return key;
    }

    /// Resolve a cicn handle (GetCIcon key) to its decoded image.
    internal static Rgba8Image? ResolveCIcon(int handle) => ResolveScratchPixmap(handle);

    /// PlotCIcon — draw colour icon `handle` (from GetCIcon), scaled into the
    /// managed {top,left,bottom,right} `rect`, masked by its alpha, in the
    /// current port. An empty rect falls back to the icon's natural size at
    /// the rect origin.
    public static void PlotCIcon(short[] rect, int handle)
    {
        if (rect is null || rect.Length < 4) return;
        var img = ResolveScratchPixmap(handle);
        if (img is null) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) rc = new RectI(rect[1], rect[0], img.Width, img.Height);
        EnqueueDraw(c => c.Blit(img, rc, RgbaColor.White));
    }

    /// DisposeCIcon — icons are boot-loaded once and reused for the session
    /// (cached by id), so disposal is a no-op (the scratch pixmap persists).
    public static void DisposeCIcon(int icon) { }

    // 'ppat' colour pixel patterns (radar-jam static, armor-bar fill).
    private const int PixPatHandleBase = 0x17000000;
    private static int _nextPixPatSlot;
    private static readonly Dictionary<int, int> _pixPatIdToHandle = new();        // ppat id → handle
    private static readonly Dictionary<int, Rgba8Image> _pixPatTiles = new();      // handle → decoded tile

    /// GetPixPat — load + decode colour pattern 'ppat' `id` into a tile, register
    /// it, and return its handle (0 if missing/undecodable). Cached per id.
    /// FillCRect tiles the decoded pattern.
    public static int GetPixPat(int id)
    {
        if (_pixPatIdToHandle.TryGetValue(id, out int cached)) return cached;
        int handle = 0;
        byte[]? bytes = GetResourceImpl?.Invoke((uint)MacResType.PixelPattern, id);
        if (bytes is not null)
        {
            var tile = PpatDecoder.Decode(bytes, $"ppat {id}");
            if (tile is not null)
            {
                handle = PixPatHandleBase + (_nextPixPatSlot++);
                _pixPatTiles[handle] = tile;
            }
        }
        _pixPatIdToHandle[id] = handle;
        return handle;
    }

    /// Resolve a ppat handle (from GetPixPat) to its decoded tile, or null.
    public static Rgba8Image? ResolvePixPat(int handle)
        => _pixPatTiles.TryGetValue(handle, out var t) ? t : null;
}
