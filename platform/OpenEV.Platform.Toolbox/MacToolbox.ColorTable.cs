using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// ColorTable subsystem. A Mac CTabHandle is modelled as a managed ManagedColorTable
// object held in a registry; the int "handle" callers pass around is a registry key
// (NOT an EvoMemory address — EvoMemory itself is gone), so GetCTable and the
// colour-table accessors hold no unmanaged memory. The key still round-trips through
// the managed GDevice/PixMap structs (MacGDevices/MacPixMaps; e.g. NewGWorld stores it
// at PixMap.pmTable) as an opaque value — that storage is the GDevice subsystem's
// concern, not the ColorTable's.
//
// Mac ColorTable shape preserved by the model: {ctSeed, ctFlags, ctSize, ColorSpec[]}
// where ColorSpec = {value, r, g, b}; ctSize is the LAST entry index (count-1), so the
// palette loops run `i <= EntryCount`.
public static partial class MacToolbox
{
    internal sealed class ManagedColorTable
    {
        public int   Seed;            // ctSeed
        public short Flags;           // ctFlags
        public short Size;            // ctSize = count - 1
        public readonly short[] Value;
        public readonly short[] R, G, B;

        public ManagedColorTable(int count)
        {
            Size = (short)(count - 1);
            Value = new short[count];
            R = new short[count];
            G = new short[count];
            B = new short[count];
        }
        public int Count => Size + 1;

        public ManagedColorTable Clone()
        {
            var c = new ManagedColorTable(Count) { Seed = Seed, Flags = Flags };
            Value.CopyTo(c.Value, 0); R.CopyTo(c.R, 0); G.CopyTo(c.G, 0); B.CopyTo(c.B, 0);
            return c;
        }
        public void CopyFrom(ManagedColorTable src)   // BlockMove of the whole table
        {
            Seed = src.Seed; Flags = src.Flags;
            int n = System.Math.Min(Count, src.Count);
            System.Array.Copy(src.Value, Value, n);
            System.Array.Copy(src.R, R, n);
            System.Array.Copy(src.G, G, n);
            System.Array.Copy(src.B, B, n);
        }
    }

    // Handle registry. Keys start above the old EvoMemory address space so a stray
    // EvoMemory.ReadInt(handle) by an un-migrated caller would have been obviously
    // wrong, not aliased (EvoMemory itself is gone now; the separation is legacy).
    private static readonly Dictionary<int, ManagedColorTable> _colorTables = new();
    private static int _nextColorTableHandle;
    private const int ColorTableHandleBase = 0x40000000;

    private static int RegisterColorTable(ManagedColorTable ct)
    {
        int handle = ColorTableHandleBase + _nextColorTableHandle++;
        _colorTables[handle] = ct;
        return handle;
    }
    internal static ManagedColorTable ResolveColorTable(int handle)
        => _colorTables.TryGetValue(handle, out var ct) ? ct : null;
    internal static bool UnregisterColorTable(int handle) => _colorTables.Remove(handle);
    internal static void ClearColorTables() { _colorTables.Clear(); _nextColorTableHandle = 0; }

    /// GetCTable — Mac trap. For a pixel DEPTH (1/2/4/8) build a synthesized palette
    /// (2^depth entries); for any other value treat the arg as a 'clut' RESOURCE ID and
    /// return that resource's decoded ColorTable handle (or 0/NULL if absent), per the Mac
    /// contract. EVO uses the depth path for the 1-bit/8-bit GWorld-creation ctabs and the
    /// resource path for the credits/intro palettes (GetCTable(1000)/(1001)).
    ///
    /// NOTE: the table VALUES are not on-screen today — every consumer (Palette.Install
    /// ScreenPalette / AnimatePaletteTransition / AnimatePaletteColorCycle) early-returns on
    /// RenderGlobals.ColorQuickDrawAvailable (never assigned → 0; offscreen GWorlds are
    /// RenderTarget-backed true colour and sprites carry their own tables). This now honours
    /// the depth-vs-resource contract correctly; ungating the palette path is separate.
    public static int GetCTable(int arg)
    {
        if (arg == 1 || arg == 2 || arg == 4 || arg == 8)
            return RegisterColorTable(BuildDepthColorTable(arg));

        // 'clut' resource id → decode the resource's ColorTable, or NULL (0) if absent.
        byte[]? bytes = GetResourceImpl?.Invoke((uint)MacResType.ColorTable, arg);
        if (bytes is null) return 0;
        var ct = DecodeClutResource(bytes);
        return ct is null ? 0 : RegisterColorTable(ct);
    }

    // Depth palette. For 8-bit this is the CANONICAL Mac 8-bit system CLUT ('clut' 8):
    // the 6×6×6 colour cube descending from white (indices 0..214, black excluded), then
    // 10-step pure ramps of red/green/blue/gray holding the ten non-cube 0x11-multiples
    // (0xEE,0xDD,0xBB,0xAA,0x88,0x77,0x55,0x44,0x22,0x11), then black at 255. The exact
    // entry SET matters on screen: the cloak screen-palette remap quantizes to these
    // entries' lightness levels, and the SheepShaver cloak capture's green histogram
    // matches this table's levels (the ramp entries contribute the half-step levels the
    // old synthesized cube+grays table lacked). Lower depths keep the grayscale ramp
    // (structural only — GWorld-creation ctabs).
    private static ManagedColorTable BuildDepthColorTable(int depth)
    {
        int count = 1 << depth;
        var ct = new ManagedColorTable(count);
        if (count == 256)
        {
            static short Ch(int v8) => (short)(v8 * 0x101);   // 8-bit -> Mac 16-bit channel
            for (int i = 0; i < 215; i++)                      // cube, white (0xFF,0xFF,0xFF) first
            {
                ct.R[i] = Ch((5 - i / 36) * 0x33);
                ct.G[i] = Ch((5 - i / 6 % 6) * 0x33);
                ct.B[i] = Ch((5 - i % 6) * 0x33);
            }
            int[] ramp = { 0xEE, 0xDD, 0xBB, 0xAA, 0x88, 0x77, 0x55, 0x44, 0x22, 0x11 };
            for (int k = 0; k < 10; k++)
            {
                ct.R[215 + k] = Ch(ramp[k]);                                            // reds
                ct.G[225 + k] = Ch(ramp[k]);                                            // greens
                ct.B[235 + k] = Ch(ramp[k]);                                            // blues
                ct.R[245 + k] = ct.G[245 + k] = ct.B[245 + k] = Ch(ramp[k]);            // grays
            }
            // index 255 stays black (0,0,0)
            for (int i = 0; i < 256; i++) ct.Value[i] = (short)i;
            return ct;
        }
        for (int i = 0; i < count; i++)                        // 1/2/4-bit: grayscale ramp
        {
            ct.Value[i] = (short)i;
            int gray = count > 1 ? i * 0xffff / (count - 1) : 0;
            ct.R[i] = (short)gray; ct.G[i] = (short)gray; ct.B[i] = (short)gray;
        }
        return ct;
    }

    // Decode a 'clut' resource: {ctSeed(4), ctFlags(2), ctSize(2), [value(2),r(2),g(2),b(2)]×}
    // into a ManagedColorTable (raw 16-bit Mac channels; no gamma — these are CLUT entries,
    // not display pixels). Returns null on a malformed/oversized table.
    private static ManagedColorTable? DecodeClutResource(byte[] d)
    {
        if (d.Length < 8) return null;
        int seed = (d[0] << 24) | (d[1] << 16) | (d[2] << 8) | d[3];
        short flags = (short)((d[4] << 8) | d[5]);
        short size = (short)((d[6] << 8) | d[7]);   // ctSize = count - 1
        int count = size + 1;
        if (count <= 0 || count > 256 || d.Length < 8 + count * 8) return null;
        var ct = new ManagedColorTable(count) { Seed = seed, Flags = flags };
        int o = 8;
        for (int i = 0; i < count; i++)
        {
            ct.Value[i] = (short)((d[o] << 8) | d[o + 1]); o += 2;
            ct.R[i]     = (short)((d[o] << 8) | d[o + 1]); o += 2;
            ct.G[i]     = (short)((d[o] << 8) | d[o + 1]); o += 2;
            ct.B[i]     = (short)((d[o] << 8) | d[o + 1]); o += 2;
        }
        return ct;
    }

    /// A fresh registry ColorTable with the given header (ctSeed/ctFlags/ctSize;
    /// NewGWorld's RGBDirect stub passes count=1, so ctSize = count-1 = 0, matching
    /// the decompile's raw header write through the master ptr — was NewHandle(8) +
    /// *(int*)*local_26=seed; *(short*)(*local_26+4)=0; *(short*)(*local_26+6)=0).
    public static int NewColorTable(int seed, short flags = 0, int count = 0)
    {
        var ct = new ManagedColorTable(count) { Seed = seed, Flags = flags };
        return RegisterColorTable(ct);
    }

    /// Clone a ColorTable handle into a new registered handle (the Mac HandToHand /
    /// new-CTabHandle idiom). Returns 0 if the source is unknown.
    public static int CloneColorTable(int srcCtHandle)
    {
        var s = ResolveColorTable(srcCtHandle);
        return s == null ? 0 : RegisterColorTable(s.Clone());
    }

    // ColorTable field accessors (registry-backed).
    public static int ColorTableEntryCount(int ctHandle) => ResolveColorTable(ctHandle)?.Size ?? 0;
    public static int ColorTableSeed(int ctHandle)       => ResolveColorTable(ctHandle)?.Seed ?? 0;
    public static void SetColorTableSeed(int ctHandle, int seed)
    {
        var ct = ResolveColorTable(ctHandle);
        if (ct != null) ct.Seed = seed;
    }
    public static void GetColorTableRGB(int ctHandle, int i, out short r, out short g, out short b)
    {
        var ct = ResolveColorTable(ctHandle);
        if (ct != null) { r = ct.R[i]; g = ct.G[i]; b = ct.B[i]; }
        else { r = g = b = 0; }
    }
    public static void SetColorTableRGB(int ctHandle, int i, short r, short g, short b)
    {
        var ct = ResolveColorTable(ctHandle);
        if (ct != null) { ct.R[i] = r; ct.G[i] = g; ct.B[i] = b; }
    }
    public static void SetColorTableEntryValue(int ctHandle, int i, short value)
    {
        var ct = ResolveColorTable(ctHandle);
        if (ct != null) ct.Value[i] = value;
    }
    // The managed table IS the entries — installing to a (stubbed) CLUT is a no-op.
    public static void SetColorTableEntries(int ctHandle, int start, int count) => SetEntries();

    // Copy src→dst RGB for entries 0..lastIndex INCLUSIVE. lastIndex is the Mac
    // ctSize (= entry count − 1); the R/G/B arrays are sized to the full count, so
    // the inclusive `<=` is correct — do NOT change it to `<`.
    public static void CopyColorTableRGB(int dstCtHandle, int srcCtHandle, int lastIndex)
    {
        var d = ResolveColorTable(dstCtHandle);
        var s = ResolveColorTable(srcCtHandle);
        if (d == null || s == null) return;
        for (int i = 0; i <= lastIndex; i++) { d.R[i] = s.R[i]; d.G[i] = s.G[i]; d.B[i] = s.B[i]; }
    }
    // Copy the whole source table into dest (the snapshot BlockMove; `size` is vestigial).
    public static void BlockMoveColorTableData(int destCtHandle, int srcCtHandle, int size)
    {
        var d = ResolveColorTable(destCtHandle);
        var s = ResolveColorTable(srcCtHandle);
        if (d != null && s != null) d.CopyFrom(s);
    }

    // GDevice / window / RGBColor struct accessors (not ColorTable registry).
    // GDevice handle → its active PixMap's ColorTable handle. Every GDevice is managed
    // (MacGDevices) and every PixMap managed (MacPixMaps), so the raw GDHandle→GDevice→
    // gdPMap→pmTable quad-deref collapses to two registry lookups.
    public static int DeviceColorTable(int gdeviceHandle)
        => GetPixMapColorTable(GetDevicePMapHandle(gdeviceHandle));

    // GDevice handle → its gdRect {top,left,bottom,right} (managed MacGDevices fields),
    // for RectRgn clipping. Returns a managed short[4] (was the raw gdRect address).
    public static short[] DeviceBoundsRect(int gdeviceHandle)
    {
        var d = MacGDevices.At(gdeviceHandle);
        return new[] { d.RectTop, d.RectLeft, d.RectBottom, d.RectRight };
    }

    // WindowRecord → next window in the window list. The game keeps a single (managed) window,
    // so the list-walk terminates immediately — return 0 (no next).
    public static int NextWindow(int window) => 0;

    // Read a Mac RGBColor (3 shorts: r@0, g@2, b@4) from a raw pointer.
    // Palette.ScreenFadeCTab/ScreenPaletteCTab are never seeded in the game, so callers
    // (FadeInFromColor/AnimatePaletteColorCycle) hand a small NON-pointer value here. A
    // real Mac handle/address is always ≥ 0x10000000 (PEF load + every registry HandleBase);
    // a smaller value is the unseeded cell → return black (the documented "treat it as
    // black" intent) instead of the near-null-address throw a raw EvoMemory read used
    // to produce (EvoMemory itself is gone now). The CLUT
    // consumers of these channels are inert in the game's true-colour renderer, so black is
    // harmless.
    public static void ReadRGBColor(int rgbPtr, out short r, out short g, out short b)
    {
        // The only RGBColor pointers handed here are Palette.ScreenFadeCTab/ScreenPaletteCTab,
        // which are NEVER seeded in the game (always a small non-pointer value); the channels feed
        // the inert CLUT path (true-colour renderer). So this always resolves to black —
        // the EvoMemory ReadShort branch was dead (no real RGBColor address ever reaches it).
        r = 0; g = 0; b = 0;
    }

    // RGB2HSL lightness field. The Mac .glue::RGB2HSL (Color Picker pkg — OS code, NOT in the
    // game binary) converts an RGBColor to HSLColor {hue, saturation, lightness}; the consumer
    // Palette.RemapToHSL feeds the LIGHTNESS field into FixRatio(entryL, targetL) to scale each
    // palette entry toward a target hue (the ASM loads both lightness shorts with lhz —
    // zero-extended). Lightness = standard HSL (max+min)/2 on the channels as UNSIGNED 16-bit
    // SmallFract values, halved in a 17-bit-safe int.
    // Ground truth (SheepShaver planet-disc capture, per-pixel aligned 2026-07-10): the cloak's
    // full-channel preset makes the remap the IDENTITY on this unsigned lightness — all 28
    // distinct Earth-disc art colours match hue×L(entry) exactly (e.g. ocean (0,0,0x8888) →
    // L 0x4444 → display 102; (0,0x9999,0x3333) → 0x4CCC → 110; grey 0xDD → 0xDDDD → 231;
    // pure green/red → 0x7FFF → 157; white → 0xFFFF → 255).
    // Keep this UNSIGNED: a signed (max+min)>>1 inverts every mixed colour with a channel
    // >= 0x8000 (Earth's oceans went bright, 213 vs 102), and max(r,g,b) is HSV value, not
    // HSL lightness.
    public static int RGB2HSLValue(short r, short g, short b)
    {
        int max = System.Math.Max((ushort)r, System.Math.Max((ushort)g, (ushort)b));
        int min = System.Math.Min((ushort)r, System.Math.Min((ushort)g, (ushort)b));
        return (max + min) >> 1;   // 0..0xFFFF
    }
}
