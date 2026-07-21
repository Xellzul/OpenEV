using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_1005b7ac (EV Override-11.c 37744-37778) — set the QuickDraw pen colour for a ship's radar dot:
// the player = cyan, a disabled hull = the frame colour, an engageable hostile = red, the player's
// own escort (or an escort of one) = neutral, anything else = blue.
public static class ResolveRadarDotColor
{
    public static void Run(ShipRec ship)
    {
        if (ship.SlotIndex == 0)
            MacToolbox.ForeColor(QuickDrawColor.Cyan);
        else if (ShipDerivedStats.IsDisabled(ship))
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
        else if (ShipAi.IsEngageableTarget(ship))
            MacToolbox.ForeColor(QuickDrawColor.Red);
        else if (ship.OwnerSlot == 0 && ship.DefendedSpobIndex == -1)
            MacToolbox.RGBForeColor((uint)UiColors.Neutral);
        else if (ship.OwnerSlot == -1)
            MacToolbox.ForeColor(QuickDrawColor.Blue);
        else if (Core.Model.GameData.Ships[ship.OwnerSlot].OwnerSlot == 0)
            MacToolbox.RGBForeColor((uint)UiColors.Neutral);
        else
            MacToolbox.ForeColor(QuickDrawColor.Blue);
    }
}
