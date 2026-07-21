using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// ONE CGrafPort as a managed C# object — the port-record slice of the (now
// complete) EvoMemory retirement. Replaces the NewPtr'd 0x6c-byte CGrafPort
// blocks the offscreen-GWorld chain allocates (NewOffscreenColorPort /
// OpenCPort): portPixMap is a MacPixMaps registry id (exactly what the raw
// record held at port+2 already), portRect is four typed shorts, visRgn/
// clipRgn are MacRegions handles.
//
// HOST-KEY INVARIANT: the MonoGame bridge keys RenderTargets/scratch textures
// by the int "pixmap key" = port + 2 (see MacToolbox.Bridge.ResolveTexture —
// a pure dictionary lookup, it never reads memory at the key). A managed port
// keeps that convention via PixmapKey => Handle + 2, so CopyBits/SetPort call
// sites and the host routing tables need no re-keying.
//
// WINDOW RECORDS (NewCWindow's 0x9c CWindowRecord with an embedded port) used
// to stay raw EvoMemory; NewCWindow now builds a managed MacGrafPort directly
// for the window (its non-port fields — visible/refCon — had no reader, so
// they were dropped rather than modelled; see MacToolbox.NewCWindow). The
// MacToolbox port accessors still dual-dispatch on MacGrafPorts.IsHandle, but
// every port the game builds today is managed/sentinel/dialog — there is no raw
// EvoMemory window branch left to coexist with.
public sealed class MacGrafPort
{
    public readonly int Handle;

    public int PixMapHandle;       // portPixMap (was port+2) — MacPixMaps registry id
    public short PortVersion;      // was port+6; 0xc000 = colour port marker
    public short RectTop, RectLeft, RectBottom, RectRight;   // portRect (was +0x10..0x16)
    public int VisRgn;             // was port+0x18 — MacRegions handle
    public int ClipRgn;            // was port+0x1c — MacRegions handle
    public int GrafProcs;          // was port+0x68 — custom QD-bottleneck procs record ptr (0 = standard)

    internal MacGrafPort(int handle) => Handle = handle;

    /// The host RenderTarget / scratch-texture key for this port (the Mac
    /// "ReadInt(portSlot)+2" CopyBits convention, preserved numerically).
    public int PixmapKey => Handle + 2;

    public int PortRectTopLeftPacked  => (RectTop << 16) | (ushort)RectLeft;
    public int PortRectBotRightPacked => (RectBottom << 16) | (ushort)RectRight;
    public void SetPortRectPacked(int topLeftPacked, int botRightPacked)
    {
        RectTop = (short)(topLeftPacked >> 16);
        RectLeft = (short)topLeftPacked;
        RectBottom = (short)(botRightPacked >> 16);
        RectRight = (short)botRightPacked;
    }
    public short[] PortRectShorts() => new[] { RectTop, RectLeft, RectBottom, RectRight };
}

// Registry mapping the int "port pointer" ported code passes around (SetPort /
// CopyBits dst / GWorld triples) to the managed object. Fresh handles live at
// 0x6c000000+ — outside the old EvoMemory-mapped 0x10xxxxxx space and the
// other registries (0x40 colour tables, 0x50 resources, 0x60 pixmaps, 0x64
// GDevices, 0x68 sprite nodes, 0x70 sprite frames, 0x74 regions, 0x78 dialogs,
// 0x7c lists). The
// fresh-handle band isn't backed by anything: an un-converted raw port
// address has no registered handle, so MacGrafPorts.At() throws instead of
// silently reading zeros the way a stale `EvoMemory.ReadInt(port + 0xNN)`
// once would have (EvoMemory itself is gone).
//
// Fixed structural GWorlds (backdrop 0x1008f6ec, status panel 0x1008f6d0,
// secondary panel 0x1008f708) register via RegisterAt(legacySlot): the legacy
// slot ADDRESS becomes the handle, so the host keys (slot+2) stay bit-identical
// and nothing in OverrideGameV2/V2TitleAdapter re-keys.
public static class MacGrafPorts
{
    public const int HandleBase = 0x6c000000;
    private const int Stride = 0x100;   // keeps Handle+2 keys clear of neighbouring handles

    private static readonly Dictionary<int, MacGrafPort> _store = new();
    private static int _nextHandle = HandleBase;

    /// OpenCPort for a managed port: fresh (empty) pixmap, empty vis/clip
    /// regions, colour-port version. The caller sets portRect and NewGWorld
    /// fills the pixmap — same contract as the raw OpenCPort.
    public static MacGrafPort NewPort()
    {
        _nextHandle += Stride;
        var port = new MacGrafPort(_nextHandle)
        {
            PortVersion = unchecked((short)0xc000),
            PixMapHandle = MacPixMaps.Register(new MacPixMap()),
            VisRgn = MacRegions.New().Handle,
            ClipRgn = MacRegions.New().Handle,
        };
        _store[port.Handle] = port;
        return port;
    }

    /// Adopt a fixed legacy GWorld slot address as a managed port handle (the
    /// slot's host pixmap key slot+2 is already registered by the host bridge).
    /// Idempotent — the panel GWorlds are wired on every game-world enter.
    public static MacGrafPort RegisterAt(int legacyHandle)
    {
        if (_store.TryGetValue(legacyHandle, out var existing)) return existing;
        var port = new MacGrafPort(legacyHandle) { PortVersion = unchecked((short)0xc000) };
        _store[legacyHandle] = port;
        return port;
    }

    /// Throws on a stale/foreign handle — the migration tripwire.
    public static MacGrafPort At(int handle) => _store[handle];
    public static bool IsHandle(int handle) => _store.ContainsKey(handle);

    /// CloseCPort for a managed port: release the pixmap + regions. The pixel
    /// CONTENTS are owned and disposed by callers beforehand (same as raw).
    public static void Dispose(int handle)
    {
        if (!_store.TryGetValue(handle, out var port)) return;
        MacPixMaps.Dispose(port.PixMapHandle);
        MacRegions.Dispose(port.VisRgn);
        MacRegions.Dispose(port.ClipRgn);
        _store.Remove(handle);
    }
}
