using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1005631c (EV Override-11.c lines 35385-35487): draw the player's shield bar (or
// the armor bar once the shield is depleted) plus the shield-status icon, into the backdrop
// panel, then composite the strip to screen. The cloak flag suppresses the shield redraw.
public static class DrawPlayerShieldBar
{
    private const double BarScale = 76.0;   // toc-0x64d8 — bar fill-width pixel scale

    public static void Run()
    {
        // Rects are managed short[4] {top, left, bottom, right}; SetRect takes (rect, left, top, right, bottom).
        var dstRect = new short[4];   // bar destination rect
        var srcRect = new short[4];   // panel source rect
        var iconRect1 = new short[4];   // shield-icon dest rect
        var iconRect2 = new short[4];   // shield-icon src rect
        var fillRect = new short[4];   // fill rect (copy of dstRect)

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(2020);
        MacToolbox.TextSize(14);
        MacToolbox.SetRect(dstRect, (short)(GlobalState.PortRight - 85), (short)(GlobalState.PortTop + 154),
                           (short)(GlobalState.PortRight - 10), (short)(GlobalState.PortTop + 160));
        MacToolbox.SetRect(srcRect, 59, 154, 134, 160);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2, srcRect, dstRect, 0, 0);

        // Two shield-icon rects over the backdrop-portRect origin.
        short iconTop = (short)(RenderGlobals.BackdropPort.RectTop + 147);
        short iconLeft = (short)(RenderGlobals.BackdropPort.RectLeft + 5);
        short iconLeft2 = (short)(RenderGlobals.BackdropPort.RectLeft + 58);
        short iconBot = (short)(RenderGlobals.BackdropPort.RectTop + 182);
        // Camera-centre X; span = 2 * centre = the full play-area width.
        short iconSpan = (short)(WorldFlags.CameraCentreX * 2);
        short iconRight = (short)(iconLeft + iconSpan);
        short iconRight2 = (short)(iconLeft2 + iconSpan);
        MacToolbox.SetRect(iconRect1, iconRight, iconTop, iconRight2, iconBot);
        MacToolbox.SetRect(iconRect2, iconLeft, iconTop, iconLeft2, iconBot);

        // +0x68 holds the numeric int shield VALUE in the float Shield slot — read as (int)Shield,
        // not the float bit pattern (matches ApplyShipDamage / TickShipAI).
        if ((int)GameData.Player.Shield < 1)
        {
            MacToolbox.CopyBits(RenderGlobals.SecondaryPanelGWorld + 2, GlobalState.ActivePortPixmap + 2, RenderGlobals.SecondaryPanelPort.PortRectShorts(), iconRect1, 0, 0);
        }
        else
        {
            MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, GlobalState.ActivePortPixmap + 2, iconRect2, iconRect1, 0, 0);
        }

        // Shield <= 0 and not dying/destroyed and not the special class 0x3f -> ARMOR bar.
        if ((int)GameData.Player.Shield < 1 &&
            !ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Player) &&
            GameData.Player.ShipClass != ShipRecord.EmptyShipClass)
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            CopyRect(fillRect, dstRect);
            short curArmor = (short)ShipDerivedStats.EffectiveArmorMax(ShipTable.Player);
            double frac = curArmor;
            int negShieldBits = -(int)GameData.Player.Shield;
            if ((double)negShieldBits <= frac)
            {
                int fillEdge = (int)-(BarScale * ((double)negShieldBits / frac) - (fillRect[1] + 76));
                fillRect[3] = (short)fillEdge;
            }
            if (RenderGlobals.ArmorBarPixPat != 0)
            {
                MacToolbox.FillCRect(fillRect, RenderGlobals.ArmorBarPixPat);
            }
        }
        // Shield > 0 (and redraw not gated by the cloak) -> SHIELD bar.
        else if (0 < (int)GameData.Player.Shield && !WorldState.IsCloaked)
        {
            MacToolbox.RGBForeColor((uint)UiColors.Friendly);
            CopyRect(fillRect, dstRect);
            uint maxShield = ShipDerivedStats.EffectiveShieldMax(ShipTable.Player);
            double frac = (int)maxShield;   // signed i2d of the uint max-shield
            int shieldBits = (int)GameData.Player.Shield;
            if ((double)shieldBits <= frac)
            {
                int fillEdge = (int)(BarScale * ((double)shieldBits / frac) + fillRect[1]);
                fillRect[3] = (short)fillEdge;
            }
            MacToolbox.PaintRect(fillRect);
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, dstRect, dstRect, 0, 0);
    }

    // Copy an 8-byte Mac Rect.
    private static void CopyRect(short[] dst, short[] src)
    {
        dst[0] = src[0]; dst[1] = src[1]; dst[2] = src[2]; dst[3] = src[3];
    }
}
