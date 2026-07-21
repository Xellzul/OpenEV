namespace OpenEV.Override.Ports.Mission.Model;

// The per-entry random odds table — one per MissionAvailTable slot, seeded
// rng(100)+1 at world init/reset (InitGameWorldState / ResetWorldStateForNewPilot),
// rolled against for spaceport bar pers/mission eligibility (IsBarPersEligible) and
// zeroed for an entry when its mission is accepted (AcceptMission). Formerly the
// heap short[512] behind PTR slot 0x10080c08 (toc-0x7a58), BSS target 0x100dfda4.
public static class RandomOddsTable
{
    public static readonly short[] Store = new short[MissionAvailTable.Count];
}
