namespace OpenEV.Platform.EvoData.Resources.Flags;

[System.Flags]
public enum PersFlags : ushort
{
    None = 0,
    Grudge = 0x0001,
    PodAndAfterburner = 0x0002,

    // The next 9 members are named from usage tracing of UpdateShipAiFrame's
    // (FUN_10025074, EV Override-11.c 16033-16085) hail-suppression gate chain, not
    // decompile-stated names.
    RequireMissionAccepted = 0x0004,
    RequireEngagingPlayer = 0x0008,
    SuppressHail = 0x0010,
    HailOnlyWhileDisabled = 0x0020,

    ReplaceShipOnMissionAccept = 0x0040,
    SayOnce = 0x0080,                        // see ShipRecord.HailQuoteSpoken's own doc comment
    DeactivateAfterMission = 0x0100,
    RequiresBarMissionEligible = 0x0400,
    LeaveAfterMissionAccept = 0x0800,
    SuppressForPlayerAiTier1 = 0x1000,       // player ShipClassTable.InherentAI == 1
    SuppressForPlayerAiTier2 = 0x2000,       // player ShipClassTable.InherentAI == 2
    SuppressForPlayerAiTierAbove2 = 0x4000,  // player ShipClassTable.InherentAI > 2
    DisasterInfo = 0x8000
}
