using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// Mac Resource Manager + heap arena.
//
// The Mac model: a resource is loaded as a Handle (pointer to a master pointer
// to the resource bytes); callers do `*handle` to reach the data and BlockMove
// bytes out of it. GetResource returns an opaque synthetic handle (see
// RegisterResource below) backed by a managed byte[] dictionary.
//
// Resource bytes come from the host via GetResourceImpl (wired in
// V2TitleAdapter to OverrideGameData). Today only STR# is wired; other
// types can be added to the host delegate without touching this file.
public static partial class MacToolbox
{
    /// Host lookup of raw Mac resource bytes by (type, id). Returns null
    /// if the resource doesn't exist. type is a 4-char-code (e.g. 'STR#'
    /// = 0x53545223). Assigned by the host (V2TitleAdapter).
    public static System.Func<uint, int, byte[]?>? GetResourceImpl;

    // Bump allocator: dedicated address-space arena used by NewPtr/NewHandle.
    // 0x10400000 sits ABOVE the TOC (_toc = 0x10301000, ±~0x8000) and well below
    // the lone high address 0x10fff000 used elsewhere. Bump-only: freed en masse
    // by Reset, never per-block (matches how the title/credits paths use it).
    private const int ArenaBase  = 0x10400000;
    private const int ArenaLimit = 0x10f00000;   // ~11 MB, ends before 0x10fff000
    private static int _arenaNext = ArenaBase;

    /// Allocate `size` bytes (4-byte aligned) from the arena; returns the arena
    /// address. internal so NewPtr/NewHandle can share it.
    internal static int ArenaAlloc(int size)
    {
        if (size <= 0) size = 1;
        size = (size + 3) & ~3;                  // 4-byte align
        int p = _arenaNext;
        if (p + size > ArenaLimit)
            throw new System.OutOfMemoryException(
                $"MacToolbox arena exhausted: 0x{p:x8} + {size} > 0x{ArenaLimit:x8}");
        _arenaNext = p + size;
        return p;
    }

    // (type,id) → Handle. Mac GetResource returns the already-loaded
    // handle on a repeat call rather than reloading.
    private static readonly Dictionary<(uint Type, int Id), int> _resourceHandles = new();
    // Reverse map: Handle → (type,id), so GetResInfo can report a resource's
    // type/id/name from just its handle (the Mac trap signature).
    private static readonly Dictionary<int, (uint Type, int Id)> _handleToTypeId = new();

    /// Host lookup of a resource's NAME by (OSType, id). Wired by the host from
    /// the parsed resource map; consumed by GetResInfo (e.g. system/ship names).
    public static System.Func<uint, int, string?>? GetResNameImpl;

    /// Host count of resources of a given OSType. The universe loader's per-type
    /// loops (syst/spob/…) early-exit once they've loaded this many, so a stub 0
    /// made them stop after the first. Typed overload binds ahead of the generic absorber stubs.
    public static System.Func<uint, int>? CountResourcesImpl;
    public static int CountResources(uint type) => CountResourcesImpl?.Invoke(type) ?? 0;
    public static int CountResources(MacResType type) => CountResources((uint)type);

    /// Host lookup of the id of the index-th (1-based, map order) resource of a
    /// type — backs the real GetIndResource (Mac semantics). Returns null when
    /// out of range. Wired by the host alongside CountResourcesImpl.
    public static System.Func<uint, int, int?>? GetIndResourceIdImpl;
    public static int GetIndResource(uint type, int index)
    {
        int? id = GetIndResourceIdImpl?.Invoke(type, index);
        return id is int rid ? GetResource(type, rid) : 0;
    }
    public static int GetIndResource(MacResType type, int index) => GetIndResource((uint)type, index);

    // Memory Manager free-list (NewPtr/NewHandle/DisposePtr): layered over the bump
    // arena so the common alloc-many-then-free-all pattern (e.g. CreditsScroller NewPtrs
    // 128 line buffers then DisposePtrs them all) REUSES blocks instead of leaking —
    // without this, every About-open would bump the arena ~32 KB and eventually exhaust
    // it. First-fit by exact aligned size (all real call sites use fixed sizes).
    private static readonly Dictionary<int, int> _blockSize = new();          // ptr → aligned size
    private static readonly Dictionary<int, int> _handlePtr = new();          // Handle → data block ptr (the master pointer, managed — was an EvoMemory cell)
    private static readonly Dictionary<int, Stack<int>> _freeBySize = new();  // size → freed ptrs

    // NO-OP: `zero` is ignored. It's retained for the NewPtr/NewPtrClear call contract, but
    // the arena hands out OPAQUE TOKENS — every consumer (FsSpec/Handle/scratch) uses the
    // address as a dict key, never reading the block's bytes — so a reused block's stale bytes
    // are unobservable and there is nothing to zero.
    private static int MemAlloc(int size, bool zero)
    {
        int aligned = ((size <= 0 ? 1 : size) + 3) & ~3;
        int ptr;
        if (_freeBySize.TryGetValue(aligned, out var bin) && bin.Count > 0)
            ptr = bin.Pop();
        else
            ptr = ArenaAlloc(aligned);
        _blockSize[ptr] = aligned;
        return ptr;
    }

    private static void MemFree(int ptr)
    {
        if (ptr == 0 || !_blockSize.TryGetValue(ptr, out int size)) return;
        _blockSize.Remove(ptr);
        if (!_freeBySize.TryGetValue(size, out var bin))
            _freeBySize[size] = bin = new Stack<int>();
        bin.Push(ptr);
    }

    /// Reset the arena + resource cache + allocator. No live caller today.
    public static void ResetResources()
    {
        _arenaNext = ArenaBase;
        _mainScreenDevice = 0;   // rebuilt by InitMainScreenDevice on next boot
        _mainScreenWidth = 0;
        _mainScreenHeight = 0;
        ClearColorTables();
        _resourceHandles.Clear();
        _handleToTypeId.Clear();
        _resourceData.Clear();
        _nextResourceHandle = 0;
        _blockSize.Clear();
        _handlePtr.Clear();
        _freeBySize.Clear();
    }

    // Memory Manager traps.
    /// NewPtr — allocate a non-relocatable block; contents undefined (Mac).
    public static int NewPtr(int size) => MemAlloc(size, zero: false);
    /// NewPtrClear — allocate a zero-filled non-relocatable block.
    public static int NewPtrClear(int size) => MemAlloc(size, zero: true);

    /// NewHandle — allocate a relocatable block, returning a Handle. The master pointer
    /// (Handle → data block) lives in the managed _handlePtr dict now (was an EvoMemory
    /// cell); no Ports code derefs `*handle` raw, so the indirection is invisible.
    public static int NewHandle(int size)
    {
        int data = MemAlloc(size, zero: false);
        int handle = MemAlloc(4, zero: false);
        _handlePtr[handle] = data;
        return handle;
    }
    /// NewHandleClear — NewHandle with zero-filled data.
    public static int NewHandleClear(int size)
    {
        int data = MemAlloc(size, zero: true);
        int handle = MemAlloc(4, zero: false);
        _handlePtr[handle] = data;
        return handle;
    }

    // GetCTable lives in MacToolbox.ColorTable.cs (managed ColorTable registry).

    /// DisposePtr — return a NewPtr block to the free-list.
    public static void DisposePtr(int ptr) => MemFree(ptr);
    /// DisposeHandle — free both the master-pointer cell and its data block.
    public static void DisposeHandle(int handle)
    {
        if (handle == 0) return;
        if (UnregisterColorTable(handle)) return;   // managed ColorTable handle, not an EvoMemory block
        if (_resourceData.Remove(handle)) return;    // managed resource handle
        if (_handlePtr.TryGetValue(handle, out int data)) { MemFree(data); _handlePtr.Remove(handle); }
        MemFree(handle);                     // handle cell
    }

    /// GetHandleSize — size of the data block a Handle points to (0 if
    /// unknown). Backed by the allocator's per-block size map.
    public static int GetHandleSize(int handle)
    {
        if (handle == 0) return 0;
        if (_resourceData.TryGetValue(handle, out var res)) return res.Length;  // managed resource
        return _handlePtr.TryGetValue(handle, out int dp) && _blockSize.TryGetValue(dp, out int sz) ? sz : 0;
    }

    // Resource Manager traps.
    // GetResource — load a resource by type+id, returning its Handle (an opaque synthetic
    // token backed by a managed byte[] — see RegisterResource). Returns 0 if absent.

    // Loaded-resource registry (managed): a loaded resource's bytes live in a managed byte[]
    // behind a synthetic handle (>= ResourceHandleBase, distinct from the arena addresses
    // above), so GetResource holds no unmanaged memory. Resource-reading ports go through the
    // ReadResource* / Resource* accessors below.
    private static readonly Dictionary<int, byte[]> _resourceData = new();
    private static int _nextResourceHandle;
    private const int ResourceHandleBase = 0x50000000;

    private static int RegisterResource(byte[] data)
    {
        int handle = ResourceHandleBase + _nextResourceHandle++;
        _resourceData[handle] = data;
        return handle;
    }
    internal static byte[]? ResolveResource(int handle)
        => _resourceData.TryGetValue(handle, out var d) ? d : null;

    /// NewHandleFromBytes — wrap a managed byte[] as a resource-style handle
    /// (data SHARED, not copied) so AddResource can serialize managed blocks
    /// (e.g. the PilotData save buffers) without an EvoMemory staging copy.
    public static int NewHandleFromBytes(byte[] data) => RegisterResource(data);

    public static int GetResource(MacResType type, int id) => GetResource((uint)type, id);

    public static int GetResource(uint type, int id)
    {
        // A managed open fork (e.g. the prefs or pilot file) serves its own
        // resources before the static host resource set, so the ported loader
        // reads the bytes it just wrote rather than a baked app resource.
        // Fork-served resources get a FRESH handle with CLONED bytes each call
        // (never the (type,id) cache): the pilot loader XORs resource data in
        // place (ScramblePilotHandle) — sharing the fork's array would corrupt the
        // on-disk copy, and a cached handle would hand the previous load's
        // already-deobfuscated block to the next pilot file.
        if (TryGetManagedResource(type, id, out var fork))
        {
            int h = RegisterResource((byte[])fork.Clone());
            _handleToTypeId[h] = (type, id);
            if (FileManagerTrace) System.Console.WriteLine($"[FM] GetResource type=0x{type:x} id=0x{id:x} → {fork.Length}b (managed-fork, fresh)");
            return h;
        }

        if (_resourceHandles.TryGetValue((type, id), out int existing))
            return existing;

        byte[]? bytes = GetResourceImpl?.Invoke(type, id);
        if (FileManagerTrace) System.Console.WriteLine($"[FM] GetResource type=0x{type:x} id=0x{id:x} → {(bytes is null ? "NULL" : $"{bytes.Length}b (host)")}");
        if (bytes is null) return 0;

        int handle = RegisterResource(bytes);
        _resourceHandles[(type, id)] = handle;
        _handleToTypeId[handle] = (type, id);
        return handle;
    }

    // Resource-data accessors (managed byte[], big-endian like the Mac in-memory layout).
    public static byte[]? ResourceBytes(int handle) => ResolveResource(handle);
    public static int ResourceLength(int handle) => ResolveResource(handle)?.Length ?? 0;
    public static byte ReadResourceByte(int handle, int byteOffset)
    {
        var d = ResolveResource(handle);
        return d != null && (uint)byteOffset < (uint)d.Length ? d[byteOffset] : (byte)0;
    }
    public static short ReadResourceShort(int handle, int byteOffset)
    {
        var d = ResolveResource(handle);
        if (d == null || (uint)(byteOffset + 1) >= (uint)d.Length) return 0;
        return (short)((d[byteOffset] << 8) | d[byteOffset + 1]);
    }
    public static int ReadResourceInt(int handle, int byteOffset)
    {
        var d = ResolveResource(handle);
        if (d == null || (uint)(byteOffset + 3) >= (uint)d.Length) return 0;
        return (d[byteOffset] << 24) | (d[byteOffset + 1] << 16) | (d[byteOffset + 2] << 8) | d[byteOffset + 3];
    }

    /// GetResInfo (managed) — return the resource's name as a C# string directly (no
    /// EvoMemory scratch buffer). Equivalent to the EvoMemory GetResInfo + FUN_10076178
    /// copy that fed a Pascal name field, for callers whose name field is now a string.
    public static string GetResInfo(int handle)
    {
        if (!_handleToTypeId.TryGetValue(handle, out var ti)) return "";
        return GetResNameImpl?.Invoke(ti.Type, ti.Id)
            ?? TryGetManagedResourceName(ti.Type, ti.Id)
            ?? "";
    }

    /// GetResId (managed) — return a resource handle's id (the &idOut half of the Mac
    /// GetResInfo trap, which the by-value transcribed ports dropped).
    public static short GetResId(int handle)
        => _handleToTypeId.TryGetValue(handle, out var ti) ? (short)ti.Id : (short)0;

    /// GetResInfo (managed Pascal byte[]) — write the resource's name as a length-prefixed
    /// Pascal string DIRECTLY into a managed byte[] (length byte + chars, zero-filled,
    /// capped at maxLen and the buffer), for name fields that are still Pascal byte[].
    /// Equivalent to the EvoMemory GetResInfo + FUN_10076178 copy, without scratch.
    public static void GetResInfo(int handle, byte[] nameDest, int maxLen)
    {
        System.Array.Clear(nameDest, 0, nameDest.Length);
        if (nameDest.Length == 0 || maxLen <= 0) return;
        string name = GetResInfo(handle);
        int cap = maxLen < nameDest.Length ? maxLen : nameDest.Length;
        int len = name.Length;
        if (len > 255) len = 255;
        if (len > cap - 1) len = cap - 1;     // leave room for the length byte
        nameDest[0] = (byte)len;
        for (int i = 0; i < len; i++)
            nameDest[1 + i] = (byte)name[i];
    }

    /// Write a length-prefixed Pascal string (length byte + chars + NUL) into a managed
    /// byte[], for earlier-transcription byte[] buffers.
    public static void WritePascalString(byte[] dst, string s, int maxLen)
    {
        System.Array.Clear(dst, 0, dst.Length);
        if (dst.Length == 0) return;
        int cap = maxLen < dst.Length ? maxLen : dst.Length;
        int len = s.Length;
        if (len > 255) len = 255;
        if (len > cap - 1) len = cap - 1;
        dst[0] = (byte)len;
        for (int i = 0; i < len; i++)
            dst[1 + i] = (byte)s[i];
    }

    /// GetIndString (managed) — return the `index`'th (1-based) Pascal string of STR#
    /// `listId` as a C# string ("" on a missing list / out-of-range index). Lets callers
    /// off the Str255 EvoMemory buffer.
    public static string GetIndString(short listId, short index)
    {
        // Honor the same search order GetResource does: a managed open fork
        // (prefs/pilot) overrides the baked host STR# list. Read-only, so no clone.
        byte[]? bytes = TryGetManagedResource(0x53545223u /* 'STR#' */, listId, out var fork)
            ? fork
            : GetResourceImpl?.Invoke(0x53545223u /* 'STR#' */, listId);
        if (bytes is null || bytes.Length < 2 || index < 1) return "";
        int count = (bytes[0] << 8) | bytes[1];
        int off = 2;
        for (int i = 1; i <= count && off < bytes.Length; i++)
        {
            int len = bytes[off] & 0xff;
            if (i == index)
            {
                int n = System.Math.Min(len, bytes.Length - off - 1);
                return n <= 0 ? "" : MacRomanToString(bytes, off + 1, n);
            }
            off += 1 + len;
        }
        return "";
    }
}
