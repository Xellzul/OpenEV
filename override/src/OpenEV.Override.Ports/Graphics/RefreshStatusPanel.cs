using OpenEV.Override.Ports.Ship;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10054f28 (EV Override-11.c lines 34816-34883): full status-panel refresh —
// re-blit the panel art into the right column of the backdrop, black-fill the radar dish
// when the player owns the IFF (colorized radar) outfit (DrawRadarHud's colorRadar mode
// draws on black instead of the stock dish art), draw the leftover "Foo" dev marker when
// the player is disabled, composite to screen, then dirty every HUD element and run the
// scheduler once.
public static class RefreshStatusPanel
{
    public static void Run()
    {
        // Rects are managed short[4] {top, left, bottom, right}.
        short[] panelRect = new short[4];
        short[] columnRect = new short[4];   // the full right column

        MacToolbox.FrontWindow();   // the decompile calls FrontWindow twice; this first result is unused
        int frontWindow = MacToolbox.FrontWindow();
        if (GlobalState.ActivePortPixmap == frontWindow)
        {
            SetGamePortAndDevice.Run();
            MacToolbox.SetRect(columnRect, GlobalState.PortLeft, GlobalState.PortTop,
                               GlobalState.PortRight, GlobalState.PortBottom);
            // The active port is a sentinel (Get* returns 0), so RectRgn no-ops on those; the
            // backdrop port carries the real region handles.
            MacToolbox.RectRgn(MacToolbox.GetPortClipRgn(GlobalState.ActivePortPixmap), columnRect);
            MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(GlobalState.ActivePortPixmap), columnRect);
            MacToolbox.RectRgn(RenderGlobals.BackdropPort.ClipRgn, columnRect);
            MacToolbox.RectRgn(RenderGlobals.BackdropPort.VisRgn, columnRect);
        }
        // The status strip spans the full window height, but the panel art (PICT 128) is only
        // 480 tall: draw it 1:1 at the top of the right column and black-fill below it.
        short stripLeft = (short)(GlobalState.PortRight - 144);
        short stripRight = (short)GlobalState.PortRight;
        short stripTop = (short)GlobalState.PortTop;
        short stripBottom = (short)GlobalState.PortBottom;
        short panelBottom = (short)(stripTop + RenderGlobals.StatusPanelPort.RectBottom);
        MacToolbox.SetRect(columnRect, stripLeft, stripTop, stripRight, stripBottom);
        MacToolbox.SetRect(panelRect, stripLeft, stripTop, stripRight, panelBottom);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2,
                            RenderGlobals.StatusPanelPort.PortRectShorts(), panelRect, 0, 0);
        if (panelBottom < stripBottom)
        {
            SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
            MacToolbox.SetRect(panelRect, stripLeft, panelBottom, stripRight, stripBottom);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(panelRect);
            SetGamePortAndDevice.Run();
        }
        bool hasIffRadar = ShipDerivedStats.HasIffRadar(ShipTable.Player);
        if (hasIffRadar)
        {
            SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
            MacToolbox.SetRect(panelRect, (short)(GlobalState.PortRight - 139), (short)(GlobalState.PortTop + 4),
                               (short)(GlobalState.PortRight - 6), (short)(GlobalState.PortTop + 138));
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(panelRect);
            SetGamePortAndDevice.Run();
        }
        if ((int)GameData.Player.Shield < 1)
        {
            MacToolbox.SetRect(panelRect, (short)(RenderGlobals.BackdropPort.RectLeft + 10), (short)(RenderGlobals.BackdropPort.RectTop + 150), (short)(RenderGlobals.BackdropPort.RectLeft + 49),
                               (short)(RenderGlobals.BackdropPort.RectTop + 163));
            SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
            MacToolbox.MoveTo(panelRect[1], panelRect[2] - 2);
            MacToolbox.RGBForeColor((uint)UiColors.AuxGreen);
            MacToolbox.PaintRect(panelRect);
            MacToolbox.RGBForeColor((uint)UiColors.Friendly);
            // "Foo" (toc-0x6500) is a leftover dev string in the original, drawn as the
            // shield-destroyed marker — faithful, not a placeholder.
            MacToolbox.DrawString("Foo");
            MacToolbox.ForeColor(QuickDrawColor.Black);
            SetGamePortAndDevice.Run();
        }
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, columnRect, columnRect, 0, 0);
        WorldState.SpawnPulseDirty = 1;
        WorldState.PlayerShieldBarDirty = 1;
        WorldState.HudStatusPanelDirty = 1;
        WorldState.HudWeaponPanelDirty = 1;
        WorldState.ShieldEnergyBarDirty = 1;
        WorldState.RadarRedrawDirty = 1;
        WorldState.WeaponSlotDirty = 1;
        RenderGlobals.HudCachedTargetClass = unchecked((short)0x8001);
        RenderGlobals.HudCachedTargetShield = unchecked((short)0x8001);
        RenderGlobals.HudCachedJamFlag = 0;
        TickHudRedrawScheduler.Run();
    }
}
