using OpenEV.Override.Ports.Ship;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10056758 (EV Override-11.c lines 35488-35577): draw the player's fuel bar into
// the backdrop panel, then — when the fuel has a fractional "energy" remainder — draw the
// narrower energy sub-bar over it, and composite the strip to screen.
public static class DrawShieldEnergyBar
{
    // Data-seg constants (toc-0x64d8 / -0x64e0 / -0x64b0):
    private const double BarScale = 76.0;   // bar fill-width scale
    private const double FuelUnitDivisor = 100.0;  // fuel-unit divisor
    private const float FuelThreshold = 0.0f;   // fuel draw threshold

    public static void Run()
    {
        // Rects are managed short[4] {top, left, bottom, right}; SetRect takes (rect, left, top, right, bottom).
        var barRect = new short[4];   // main bar rect
        var srcRect = new short[4];   // panel source rect
        var fillRectA = new short[4];   // fuel fill rect
        var fillRectB = new short[4];   // energy fill rect

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(2020);
        MacToolbox.TextSize(14);
        MacToolbox.SetRect(barRect, (short)(GlobalState.PortRight - 85), (short)(GlobalState.PortTop + 170),
                           (short)(GlobalState.PortRight - 10), (short)(GlobalState.PortTop + 176));
        MacToolbox.SetRect(srcRect, 59, 170, 134, 176);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2, srcRect, barRect, 0, 0);
        if (FuelThreshold < GameData.Player.Fuel)
        {
            if (!ShipDerivedStats.IsDisabled(ShipTable.Player))
            {
                MacToolbox.RGBForeColor((uint)UiColors.Friendly);
                CopyRect(fillRectA, barRect);
                short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Player);
                int fillEdge = (int)(BarScale * (GameData.Player.Fuel / (double)fuelMax) + fillRectA[1]);
                fillRectA[3] = (short)fillEdge;
                MacToolbox.PaintRect(fillRectA);
                short fuelUnits = (short)(int)(GameData.Player.Fuel / FuelUnitDivisor);
                fillEdge = (int)(GameData.Player.Fuel - (float)fuelUnits);
                if (0 < (short)fillEdge)
                {
                    CopyRect(fillRectB, fillRectA);
                    short energyMax = (short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Player);
                    fillEdge = (int)(BarScale * ((double)(fuelUnits * 100) / energyMax) + fillRectB[1]);
                    fillRectB[1] = (short)fillEdge;   // energy bar grows from the left field
                    MacToolbox.RGBForeColor((uint)UiColors.Radar);
                    MacToolbox.PaintRect(fillRectB);
                }
            }
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, barRect, barRect, 0, 0);
    }

    // Copy an 8-byte Mac Rect.
    private static void CopyRect(short[] dst, short[] src)
    {
        dst[0] = src[0]; dst[1] = src[1]; dst[2] = src[2]; dst[3] = src[3];
    }
}
