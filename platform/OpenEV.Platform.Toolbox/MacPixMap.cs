using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// ONE Mac PixMap as a managed C# object — the first slice of the (now
// complete) EvoMemory retirement. Replaced the 0x32-byte PixMap struct that
// used to live behind an EvoMemory handle (port+2): the pixel buffer is a
// byte[], every struct field is a typed member. The pmTable colour table
// (MacToolbox.ManagedColorTable) and the CGrafPort/GDevice records around the
// pixmap (MacGrafPort/MacGDevice) were divorced from EvoMemory in later
// slices — none of it is EvoMemory-backed anymore.
public sealed class MacPixMap
{
    // The pixel buffer (was the NewPtr'd baseAddr block; null = none/disposed).
    // PixelOrigin is the depth-alignment nudge NewGWorld bakes into baseAddr
    // (+4 at 1-bit, +8 at 8-bit): pixel (x,y) lives at
    // Pixels[PixelOrigin + y*RowBytes + ...]. Kept as data for bug-fidelity —
    // the old "unwind the nudge before DisposePtr" dance collapses to
    // `Pixels = null`.
    public byte[]? Pixels;
    public int PixelOrigin;

    // BRIDGE FIELD for pixmaps pointed at an EXTERNAL raw pixel buffer (the
    // render context's Install*GWorldPort aims the compose/secondary pixmaps at
    // raw sprite-pipeline blocks — see GWorldPort.InstallGWorldPortCore; not
    // EvoMemory, which is gone). 0 when Pixels is the backing. Goes away when
    // the pixel buffers themselves are divorced.
    public int LegacyBaseAddr;

    public ushort RowBytes;        // low 14 bits (the 0x8000 "is a PixMap" marker is implicit)
    public short BoundsTop, BoundsLeft, BoundsBottom, BoundsRight;
    public short PmVersion;
    public short PackType;
    public int PackSize;
    public int HRes = 0x480000;    // Fixed 72.0 dpi
    public int VRes = 0x480000;
    public short PixelType;        // 0 = indexed, 0x10 = RGBDirect
    public short PixelSize;        // depth
    public short CmpCount;
    public short CmpSize;
    public int PlaneBytes;
    public int ColorTableHandle;   // pmTable — Mac CTabHandle (registry key into MacToolbox.ManagedColorTable, not EvoMemory)
    public int PmReserved;

    public int BoundsTopLeftPacked  => (BoundsTop << 16) | (ushort)BoundsLeft;
    public int BoundsBotRightPacked => (BoundsBottom << 16) | (ushort)BoundsRight;
    public void SetBounds(int topLeftPacked, int botRightPacked)
    {
        BoundsTop = (short)(topLeftPacked >> 16);
        BoundsLeft = (short)topLeftPacked;
        BoundsBottom = (short)(botRightPacked >> 16);
        BoundsRight = (short)botRightPacked;
    }
}

// Registry mapping the int "PixMap handle" the Mac code stores in port records
// (port+2) / GDevice records (gdPMap) to the managed object. Handles live at
// 0x60000000+ — far outside the old EvoMemory-mapped 0x10xxxxxx space, so any
// un-migrated raw handle access finds nothing registered and MacPixMaps.At()
// throws, instead of silently aliasing a real global the way a stale
// `EvoMemory.ReadInt(pixMapHandle)` deref once would have (EvoMemory itself
// is gone).
public static class MacPixMaps
{
    public const int HandleBase = 0x60000000;
    private static readonly Dictionary<int, MacPixMap> _store = new();
    private static int _nextHandle = HandleBase;

    public static int Register(MacPixMap pixMap)
    {
        _nextHandle += 0x10;
        _store[_nextHandle] = pixMap;
        return _nextHandle;
    }

    /// Throws on a stale/foreign handle — the migration tripwire.
    public static MacPixMap At(int handle) => _store[handle];
    public static bool IsHandle(int handle) => _store.ContainsKey(handle);
    public static void Dispose(int handle) => _store.Remove(handle);
}
