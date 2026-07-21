using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat.Model;

namespace OpenEV.Override.Ports.Core.Model;

// Additional galaxy/world tables, formerly pointer-slot heap tables (Base =>
// ReadInt(slot), then Base + index*Stride + field) like ShipTable/SystTable, now
// fully managed. Names from the record shapes + usage (new-pilot world reset +
// the spaceport BBS).

// 512 pers ('përs' named-captain) records, stride 0x1c0, formerly behind PTR slot
// 0x1008a524; +0x19e/+0x19f are the per-pers availability / accepted flags
// InitializeNewPilotWorld resets. (The old "planet/class" note on 0x1008a524 was
// wrong, and the former "MissionTable" name was a MISNOMER — this is the pers
// table, indexed by ship.PersIndex; 512 == MaxPers.)
public static class PersTable
{
    public const int Count = 0x200;

    // The records now live in typed managed objects (PersRecord).
    public static readonly PersRecord[] Store = CreateStore();
    private static PersRecord[] CreateStore()
    {
        var s = new PersRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new PersRecord();
        return s;
    }

    // Legacy named accessors (now Store-backed).
    public static short Coward(int index) => Store[index].Coward;
    public static ushort Flags(int index) => (ushort)Store[index].Flags;
    public static short HailQuote(int index) => Store[index].HailQuote;
    public static byte AcceptedFlag(int index) => Store[index].AcceptedFlag;
}

// 8 beam slots, stride 0x1c — the live laser/beam-segment table AllocateBeamSlot
// fills and the laser-trail / hyperspace-lane draws walk. (The old
// "ActiveMissionTable" name was a shape-guess MISNAME.) Records are typed managed
// now (was PTR slot 0x1008a514, toc+0x1eb4, alloc 0xe0).
public static class BeamTable
{
    public const int Count = 8;

    public static readonly BeamRecord[] Store = CreateStore();
    private static BeamRecord[] CreateStore()
    {
        var s = new BeamRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new BeamRecord();
        return s;
    }
}

// One beam/laser segment (formerly 0x1c bytes in the BeamTable heap).
public sealed class BeamRecord
{
    // 0xfffe in the decompile — the slot is dead / free.
    public const short Killed = -2;

    public short StartX;        // +0x00  current segment start (screen space)
    public short StartY;        // +0x02
    public short EndX;          // +0x04  current segment end
    public short EndY;          // +0x06
    public short PrevStartX;    // +0x08  previous-frame segment (the laser trail)
    public short PrevStartY;    // +0x0a
    public short PrevEndX;      // +0x0c
    public short PrevEndY;      // +0x0e
    public short Life;          // +0x10  frames remaining (-2 = free slot)
    public short WeaponType;    // +0x12  WeaponTable index (draw style/width)
    public short OwnerSlot;     // +0x14  firing ship slot (-1 reset)
    public short TargetSlot;    // +0x16  target ship slot (-1 = none)
    public short FixedRange;     // +0x18
    public byte SourceShip;    // +0x1a  source-ship byte AllocateBeamSlot stores
}

// The 'jünk' special-commodity table — 128 records, formerly 0x4a bytes each in
// the heap behind PTR slot 0x1008a548 (toc+0x1ee8, alloc 0x2500). (The old
// "JumpRouteTable" name was a shape-guess MISNAME.) Records are typed managed now.
public static class JunkTable
{
    public const int Count = 0x80;

    public static readonly JunkRecord[] Store = CreateStore();
    private static JunkRecord[] CreateStore()
    {
        var s = new JunkRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new JunkRecord();
        return s;
    }
}

// One junk/special commodity (offsets = the old record layout; 'jünk' resource
// offsets noted). The trade dialog matches NavTargetSpob: BoughtAtSpob match →
// price × multiplier (the HIGH/sell-here tab 6); SoldAtSpob match → price ÷
// multiplier (the LOW/buy-here tab 7).
public sealed class JunkRecord
{
    public short SoldAtSpob;    // +0x00  res+0, − 0x80 normalized (-1 = none)
    public short BoughtAtSpob;  // +0x02  res+2, − 0x80 normalized (-1 = none)
    public short BasePrice;     // +0x04  res+4
    public short PlayerQty;     // +0x06  tons the player holds (counts toward carried mass)
    public short Flags;    // +0x08  res+6 (bit 0 gates the TickShipAI cheat/credit tick)
    public string Name = "";    // +0x0a  the resource name (was a Pascal buffer)
}

// Projectile slot table, 128 slots, stride 0x26, formerly behind PTR slot
// 0x1008a53c. The records now live in the typed managed Store (ProjectileRecord).
public static class ProjectileTable
{
    public const int Count = 0x80;

    public static readonly ProjectileRecord[] Store = CreateStore();
    private static ProjectileRecord[] CreateStore()
    {
        var s = new ProjectileRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new ProjectileRecord();
        return s;
    }
}

// One escape-pod / debris slot (formerly 0x18 bytes in the heap behind ptr
// cell 0x1008a538). The old "address-coupled into the sprite node" boundary is
// MOOT: the pod node's update UPP was never ported (InvokeNodeUpdateUpp default
// no-op), so nothing walks the record by address — the node now stores the
// SLOT INDEX in ObjectPtr for the future ported updater.
public sealed class DebrisRecord
{
    // 0xfffe in the decompile — the slot is dead; spawners scan for <= this.
    public const short Killed = -2;

    public float PosX;          // +0x00
    public float PosY;          // +0x04
    public float VelX;          // +0x08
    public float VelY;          // +0x0c
    public short LifeRemaining; // +0x10 (-2 = free)
    public short SystemId;      // +0x12
    public short AnimFrame;     // +0x14
    public short SpinDir;       // +0x16
}

public static class DebrisTable
{
    public const int Count = 0x10;

    public static readonly DebrisRecord[] Store = CreateStore();
    private static DebrisRecord[] CreateStore()
    {
        var s = new DebrisRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new DebrisRecord();
        return s;
    }
}

// The 4 background-nebula galaxy-map records ('nëbu' 0x80..0x83, loaded by
// LoadNebulaResources): a map-space rect + the per-pilot "charted" flag the
// galaxy map sets when the player visits a system inside it. (The old
// "SysObjAnimTable" name was a shape-guess MISNAME.) Records are typed managed
// now (was PTR slot 0x1008a530, toc+0x1ed0 / toc[0x7b4], alloc 0x28).
public static class MapNebulaTable
{
    public const int Count = 4;

    public static readonly MapNebulaRecord[] Store = CreateStore();
    private static MapNebulaRecord[] CreateStore()
    {
        var s = new MapNebulaRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new MapNebulaRecord();
        return s;
    }
}

// One nebula map record (formerly 10 bytes in the MapNebulaTable heap).
public sealed class MapNebulaRecord
{
    public short X;        // +0x00  map-space left
    public short Y;        // +0x02  map-space top
    public short Width;    // +0x04
    public short Height;   // +0x06
    public byte Charted;  // +0x08  1 once a system inside the rect is charted
}

// &DAT_1008f8fa: the two 0x200-entry mission/person availability lists, one per
// mode ([0] = mission BBS, [1] = bar; 0x1008fcfa was the bar half; the old
// "per-system grid" note was a misread — the first index is the InBarFlag mode).
// Rebuilt by Mission.RefreshMissionAvailabilityTables (eligible, not-already-
// active 'bär' persons per mode). Managed now.
public static class MissionAvailGrid
{
    public const int Count = 0x200;
    public static readonly short[][] ByMode = { new short[Count], new short[Count] };
}
