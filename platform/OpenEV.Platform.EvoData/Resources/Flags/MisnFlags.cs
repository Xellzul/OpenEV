namespace OpenEV.Platform.EvoData.Resources.Flags;

// mïsn "Flags" word (resource +0x50; loaded to the runtime mission record +0x58).
// AuxShipsReplacedWhenDestroyed (0x10) is decompile-confirmed. AutoAborting (0x1) is
// confirmed by USAGE TRACING, not a decompile-stated name: ApplyMissionCompletionBits
// (FUN_10051c90) has exactly 3 callers in the whole port — SpawnFleetShips.cs (x2) and
// RunFleetSpawner.cs (x1) — and ALL THREE are gated on `Flags & 1`; the function always
// ends with an unconditional AbortMission.Run(...). So bit 0 is the sole, exclusive
// trigger for "apply this mission's completion bits, then automatically tear the mission
// down" during the per-frame spawn-maintenance tick (not from any player action) — the
// TMPL's "AutoAborting" label fits that behaviour. The remaining bit names below are
// still raw TMPL/docs guesses, NOT verified against the decompile — don't rely on them
// without checking the code first.
[System.Flags]
public enum MisnFlags : ushort
{
    None = 0,
    AutoAborting = 0x0001,   // usage-traced, see header comment above
    HideRedMapArrows = 0x0002,
    Unrefusable = 0x0004,
    // SpawnFleetShips: when set, the aux-ship spawn budget (RemainingSpawnCount) is
    // refilled and never decremented on spawn, so dead aux ships are continually
    // replaced up to AuxShipCount.
    AuxShipsReplacedWhenDestroyed = 0x0010,
    // Reward-rollback flags — usage-traced (RunMissionInfoDialog item 5 / ApplyMissionFailure),
    // NOT decompile-stated: when the mission is aborted/failed its granted rewards are clawed back.
    RemoveGrantedOutfitOnAbort = 0x0020,   // confiscate the pay-granted outfit (Pay < -30127)
    RemoveReputationOnAbort = 0x0040,      // undo the per-govt system-status/reputation gain (gated on CargoType != -1)
    ShowGreenArrowInBrief = 0x0100,
    // Name derived from usage tracing (sole reader = the galaxy-map dialog's route-list build),
    // NOT decompile-stated: when set (and HideRedMapArrows clear, with a valid visible DestSystem
    // and SpawnCount > 0), the mission's spawn-destination system is added to the map route list.
    ShowDestSystemOnMap = 0x0200,
    Critical = 0x1000,
    BanFreighterPlayer = 0x2000,
    BanWarshipPlayer = 0x4000
}
