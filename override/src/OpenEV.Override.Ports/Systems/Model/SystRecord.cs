namespace OpenEV.Override.Ports.Systems.Model;

// Typed managed C# object for ONE star-system record (formerly 0x74 bytes in the
// EvoMemory byte-dictionary). One instance per slot, held in SystTable.Store[1000];
// the SystRec handle reads/writes these fields — there is no raw byte-memory
// backing anymore (EvoMemory itself was retired once every port site migrated off
// raw offset access).
//
// Offsets not promoted to a field have NO storage — SystRec's generic byte
// accessors throw for them, the migration tripwire.
public sealed class SystRecord
{
    public short XPos;                       // +0x00  X position
    public short YPos;                       // +0x02  Y position
    public short Govt;                        // +0x04  owning government (−1 none)
    public const int HyperLinkCount = 16;          // hyperspace links per system
    public short[] HyperLink = new short[HyperLinkCount];   // +0x06..+0x24  hyperspace link systems
    public const int StellarLinkCount = 4;         // stellar/spaceport links per system
    public short[] StellarLink = new short[StellarLinkCount];   // +0x26..+0x2c  stellar-object / destination links
    public const int FleetSpawnCount = 9;          // fleet/NPC spawn config slots per system
    // +0x2e..+0x3e  fleet/NPC spawn config (9 shorts): [0..3] = pers/fleet type
    // references (−0x80-biased by the loader), [4..7] = per-type spawn chances
    // (cumulative weights in RunFleetSpawner), [8] = AverageShips. (e.g. Sol =
    // [15,1,2,8, 15,30,30,25, 10]; a frontier system like Kirrim differs in the
    // weights.)
    public short[] FleetSpawn = new short[FleetSpawnCount];
    public short Visited;                       // +0x40  runtime visited/known flag
    public short Message;                         // +0x42  syst Message (news/text resource id; −1 = none)
    public short AsteroidCount;                   // +0x44  asteroid-field density (read by Systems.Asteroids Init/Tick)
    public short Interference;                    // +0x46  radar static / "murk" level (Sol 21, Kirrim 40)
    public short Visibility;                      // +0x48  EV "Visibility" control value
    public const int ForcedPersCount = 4;          // forced-pers slots per system
    // +0x4a..+0x50  ForcedPers[4]: pers (përs) resource indices this system always tries to
    // spawn (res 0x48 ×4 shorts, -0x80-biased, only in records >= 0x60 bytes; -1 = empty).
    // RunFleetSpawner (FUN_10064b58) checks each != -1, verifies the pers's AvailableFlag,
    // then SpawnPers.Run(currentSystem, 0, persIndex). (The pers table is PersTable —
    // resource 'përs' — not the mission-availability table.)
    public short[] ForcedPers = new short[ForcedPersCount];
    public byte[] Name = new byte[31];         // +0x52  C-string name
    public byte ShownFlag;                        // +0x72  "defined / in-play" flag
}
