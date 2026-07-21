using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Ship;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1004b970 (EV Override-11.c 31214-31236): returns whether the ship has room
// for `quantity` more cargo units, else posts the "not enough cargo space" alert. The first
// arg is unused - vestigial in the original.
public static class ValidateMissionCargoSpace
{
    public static bool Run(int _, short quantity)
    {
        if (quantity > 0)
        {
            short cargoMax = (short)ShipDerivedStats.EffectiveCargoMax();
            if (cargoMax < quantity)
            {
                AlertText.Message = "Your ship doesn’t have enough cargo space to load this cargo.";
                DoSceneTransition.Run(0, 0);
                RepaintGameWindow.Run();
                return false;
            }
            short freeCargo = (short)FreeCargoSpaceWithMissions.Run();
            if (freeCargo < quantity)
            {
                AlertText.Message = "Your ship doesn’t have enough free cargo space to load this cargo. Sell or jettison some and try again.";
                DoSceneTransition.Run(0, 0);
                RepaintGameWindow.Run();
                return false;
            }
        }
        WorldState.HudStatusPanelDirty = 1;
        return true;
    }
}
