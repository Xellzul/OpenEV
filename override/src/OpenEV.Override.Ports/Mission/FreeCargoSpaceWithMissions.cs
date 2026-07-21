using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1005fe5c (EV Override-11.c lines 40004-40036).
public static class FreeCargoSpaceWithMissions
{
    public static int Run()
    {
        int cargoMax = ShipDerivedStats.EffectiveCargoMax();
        int totalWithEscorts = TotalMassWithEscorts.Run();
        int carried = ShipDerivedStats.TotalMassCarried(ShipTable.Player);
        int missionMass = 0;
        for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
        {
            if (GameData.MissionStates[i].IsActive != 0 &&
                GameData.Missions[i].CargoPickedUp != 0)
            {
                missionMass += GameData.Missions[i].CargoMass;
            }
        }
        if ((short)cargoMax < (short)totalWithEscorts)
        {
            carried = (carried - missionMass) - (totalWithEscorts - cargoMax);
            if ((short)carried < 0)
            {
                carried = 0;
            }
            carried += missionMass;
        }
        return cargoMax - carried;
    }
}
