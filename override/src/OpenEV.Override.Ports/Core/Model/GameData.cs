using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Combat.Model;

namespace OpenEV.Override.Ports.Core.Model;

// Unified facade over the typed managed game-data tables. Each property returns
// the table's backing record array (class instances), so call sites read like
// `GameData.Missions[x].TargetSpob` and — because these are reference records,
// not the handle struct — WRITE the same way without the rvalue-struct dance:
// `GameData.Missions[x].TargetSpob = y`. Index i is the record slot (identical
// to XxxTable.Store[i]).
//
// For migration bridging — wrapping a legacy raw `int ptr` from an un-converted
// port — keep using `XxxTable.FromPtr(ptr)`; GameData is for clean record access.
public static class GameData
{
    public static ShipRecord[] Ships => ShipTable.Store;

    // The player's own ship (slot 0). Record class, so reads AND writes directly:
    // `GameData.Player.Credits = n`. (Ship.Model.ShipTable.Player returns the ShipRec
    // handle instead — use that only as a pointer-bridge to un-migrated raw ports.)
    public static ShipRecord Player => ShipTable.Store[0];

    public static ShipClassRecord[] ShipClasses => ShipClassTable.Store;
    public static OutfitRecord[] Outfits => OutfitTable.Store;
    public static SpobRecord[] Spobs => SpobTable.Store;
    public static MissionRecord[] Missions => MissionTable.Store;
    public static MissionStateRecord[] MissionStates => MissionStateTable.Store;

    public static SystRecord[] Systems => SystTable.Store;
    public static GovtRecord[] Governments => GovtTable.Store;
    public static MapNebulaRecord[] MapNebulas => MapNebulaTable.Store;
    public static MovieRecord[] Movies => MovieTable.Store;
    public static CronRecord[] Crons => CronTable.Store;
    public static PersRecord[] Pers => PersTable.Store;
    public static MissionAvailRecord[] MissionAvail => MissionAvailTable.Store;
    public static MissionDefRecord[] MissionDefs => MissionDefTable.Store;
    public static DudeSpawnRecord[] DudeSpawns => DudeSpawnTable.Store;
    public static BeamRecord[] Beams => BeamTable.Store;
    public static JunkRecord[] Junk => JunkTable.Store;
    public static ProjectileRecord[] Projectiles => ProjectileTable.Store;
    public static DebrisRecord[] Debris => DebrisTable.Store;
    public static WeaponRecord[] Weapons => WeaponTable.Store;
    public static AsteroidParticle[] Asteroids => AsteroidTable.Store;
    public static FleetRecord[] Fleets => FleetTable.Store;
    public static NebulaRecord[] Nebulas => NebulaTable.Store;

    // Per-entry random odds table (short[512]); see RandomOddsTable header comment.
    public static short[] RandomOdds => RandomOddsTable.Store;

    // Active key-binding map (short[45] keycodes). Indexed access is read/write;
    // Keymap.Slot()/SetSlot() remain the width-safe shorthand.
    public static short[] KeyMap => Keymap.Store;

    // ── Spaceport bribe/price scalars ──
    // Migrated from the data-seg cells 0x10086ae8 / 0x10086aec (slot consts
    // documented in Dialog.Model.DialogScratch) to managed storage. One spaceport
    // interaction is active at a time, so plain statics; the bribe / buy-ship / refuel
    // flows read & write via these properties.
    public static int BribeFine { get; set; }   // fine/bribe amount (clamped 1000..20000)
    public static int BuyShipPriceCell { get; set; }   // buy/sell/bribe price cell

    // ── EVO RNG ──
    // Lehmer/MINSTD generator state (migrated from the data-seg uint at 0x10082218,
    // which had no other accessors). Read-modify-written by Misc.SeedEvoRng.
    public static uint EvoRngState { get; set; }

    // ── Active alert/update DialogPtr ──
    // Migrated off the data-seg ptr-of-ptr at 0x10080c64: every alert/news modal
    // shares this one cell since only one is ever up at a time.
    // Starts 0 (no dialog at boot).
    public static int AlertDialog { get; set; }
}
