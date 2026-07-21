// Root namespace so unqualified `MissionCargoPickupMode` resolves in every
// OpenEV.Override.Ports.* file, same convention as ShipAiType.
namespace OpenEV.Override.Ports;

// When the mission's cargo is picked up (MissionRecord.PickupMode, +0x14, res+0x14 mïsn
// "PickupMode"). Names + values are the mïsn TMPL's PickupMode choice list
// (editor/src/OpenEV.Editor.Schema/Schemas.More.cs, MissionPickupModes), confirmed against
// every consumer: AcceptMission.cs (AtMissionStart sets CargoPickedUp on accept),
// CheckMissionEncounter.cs (AtDestination sets it on arrival at TargetSpob),
// TickShipAI.cs (WhenBoardingSpecialShip sets it when the special ship is boarded).
public enum MissionCargoPickupMode : short
{
    Ignored = -1,
    AtMissionStart = 0,
    AtDestination = 1,
    WhenBoardingSpecialShip = 2,
}
