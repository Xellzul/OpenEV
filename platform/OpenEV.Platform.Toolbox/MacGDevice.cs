using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// ONE GDevice as a managed C# object — replaces the NewHandle(0x3e) GDevice
// records NewGDeviceForPixmap builds around offscreen pixmaps (and the
// synthetic main-screen device). gdPMap was ALREADY a MacPixMaps registry id
// in the raw record (+0x16); here every field is typed and the GDHandle →
// GDevice master-pointer indirection disappears (the handle IS the device).
public sealed class MacGDevice
{
    public readonly int Handle;

    public short GdType;           // was +0x04: 0 = indexed CLUT, 2 = direct RGB
    public int ITableHandle;       // was +0x06: gdITable inverse-table handle
    public short ResPref;          // was +0x0a: gdResPref (4)
    public int PMapHandle;         // was +0x16: gdPMap — MacPixMaps registry id
    public short RectTop, RectLeft, RectBottom, RectRight;   // gdRect (was +0x22..0x28)
    public int GdMode = -1;        // was +0x2a: gdMode (NewGDeviceForPixmap stores -1)

    internal MacGDevice(int handle) => Handle = handle;

    public int RectTopLeftPacked  => (RectTop << 16) | (ushort)RectLeft;
    public int RectBotRightPacked => (RectBottom << 16) | (ushort)RectRight;
}

// Registry mapping the int "GDHandle" ported code passes (SetGWorld device
// args, GWorld triples, the cached main device) to the managed object.
// Handles at 0x64000000+ (see MacGrafPort for the handle-band map).
public static class MacGDevices
{
    public const int HandleBase = 0x64000000;
    private const int Stride = 0x40;

    private static readonly Dictionary<int, MacGDevice> _store = new();
    private static int _nextHandle = HandleBase;

    public static MacGDevice New()
    {
        _nextHandle += Stride;
        var dev = new MacGDevice(_nextHandle);
        _store[_nextHandle] = dev;
        return dev;
    }

    /// Throws on a stale/foreign handle — the migration tripwire.
    public static MacGDevice At(int handle) => _store[handle];
    public static bool IsHandle(int handle) => _store.ContainsKey(handle);
    public static void Dispose(int handle) => _store.Remove(handle);
}
