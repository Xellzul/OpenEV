using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10012308 (EV Override-11.c lines 9217-9365): the spaceport/comm dialog's
// planet info cell — draws the nav-target spob name box (item 4), its planet sprite
// (item 5, blitted from the anim-scratch GWorld), and the name/"Status:" text block
// (item 6) into the BACKDROP GWorld, then composites onto the dialog window.
public static class DrawShipInfoPanel
{
    public static void Run()
    {
        int spriteCenter = default;             // packed {Y<<16 | X} blit point
        var itemKind = new short[1];            // GetDialogItem out-params (never read back)
        var itemHandle = new int[1];
        // Managed {top@0,left@1,bottom@2,right@3} short[4] Rects.
        var itemRect = new short[4];            // the item Rect
        var itemRectCopy = new short[4];        // inset text-box copy
        var spriteRect = new short[4];          // origin-translated blit src rect

        int highlightColor = UiColors.DialogFore;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        int dlg = DialogScratch.SpaceportCommDialogRecord;
        // itemRect = dialog contentRect (window record +0x10..0x16).
        var portRect = MacToolbox.GetDialogPortRect(dlg);
        itemRect[0] = portRect[0];
        itemRect[1] = portRect[1];
        itemRect[2] = portRect[2];
        itemRect[3] = portRect[3];
        MacToolbox.InsetRect(itemRect, 1, 1);
        MacToolbox.PaintRect(itemRect);
        MacToolbox.RGBForeColor((uint)highlightColor);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(dlg));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        RenderPlanetCommButtonRow.Run(-1);
        MacToolbox.GetDialogItem(dlg, 4, itemKind, itemHandle, itemRect);
        bool itemVisible = MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dlg));
        if (itemVisible)
        {
            string navName = DialogScratch.SpaceportHailText;
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.PaintRect(itemRect);
            itemRectCopy[0] = itemRect[0]; itemRectCopy[1] = itemRect[1];
            itemRectCopy[2] = itemRect[2]; itemRectCopy[3] = itemRect[3];
            MacToolbox.InsetRect(itemRectCopy, 4, 2);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.TETextBox(navName, itemRectCopy, 0);
            MacToolbox.InvertRect(itemRect);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        int navSpob = GameData.Player.NavTargetSpob;
        MacToolbox.GetDialogItem(dlg, 5, itemKind, itemHandle, itemRect);
        itemVisible = MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dlg));
        if (itemVisible)
        {
            int spriteRec = PlanetSpriteRecordTable.Store[GameData.Spobs[navSpob].SpriteId];
            // spriteCenter (local_52) = {itemRect centre - sprite dim/2} per axis,
            // X in the LOW half (left+right), Y in the HIGH half (top+bottom).
            ushort spriteDim = (ushort)MacRectWidth.Run(spriteRec);
            uint centerSum = (uint)((int)itemRect[1] + (int)itemRect[3]);
            spriteCenter = (spriteCenter & ~0xffff) |
                           ((short)(((short)((int)centerSum >> 1) + (((int)centerSum < 0 && (centerSum & 1) != 0) ? 1 : 0))
                                    - (((short)spriteDim >> 1) + (((short)spriteDim < 0 && (spriteDim & 1) != 0) ? 1 : 0))) & 0xffff);
            spriteDim = (ushort)MacRectHeight.Run(spriteRec);
            centerSum = (uint)((int)itemRect[0] + (int)itemRect[2]);
            spriteCenter = (((short)(((short)((int)centerSum >> 1) + (((int)centerSum < 0 && (centerSum & 1) != 0) ? 1 : 0))
                                    - (((short)spriteDim >> 1) + (((short)spriteDim < 0 && (spriteDim & 1) != 0) ? 1 : 0))) << 16)) |
                           (spriteCenter & 0xffff);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            // spriteRect = itemRect copy translated to the origin (offset by -left, -top);
            // src rect for the anim-scratch -> backdrop CopyBits below.
            spriteRect[0] = itemRect[0]; spriteRect[1] = itemRect[1];
            spriteRect[2] = itemRect[2]; spriteRect[3] = itemRect[3];
            short rectLeft = itemRect[1];
            short rectTop = itemRect[0];
            MacToolbox.OffsetRect(spriteRect, (short)-rectLeft, (short)-rectTop);
            MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, RenderGlobals.BackdropGWorld + 2, spriteRect, itemRect, 0, 0);
            Graphics.BlitSpriteToBuffer.Run(Combat.Model.PlanetSpriteRecordTable.Store[GameData.Spobs[navSpob].SpriteId] /* WARNING-FIXED: was the CELL address (dropped deref) */, RenderGlobals.BackdropGWorld, spriteCenter, false);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.InsetRect(itemRect, -5, -5);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        MacToolbox.GetDialogItem(dlg, 6, itemKind, itemHandle, itemRect);
        itemVisible = MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dlg));
        if (itemVisible)
        {
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.DrawString(GameData.Spobs[navSpob].Name);
            MacToolbox.MoveTo(itemRect[1], itemRect[0] + 26);
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(DialogScratch.SpaceportDescText);
            if ((GameData.Spobs[navSpob].Flags & 0x20) == 0)
            {
                if (GameData.Spobs[navSpob].TradingEnabled == 0)
                {
                    if (DialogScratch.CommHailGateFlag != 0)
                    {
                        MacToolbox.MoveTo(itemRect[1], itemRect[0] + 42);
                        MacToolbox.RGBForeColor((uint)highlightColor);
                        MacToolbox.DrawString("Status: ");
                        MacToolbox.TextFace(1);
                        if (GalaxyMapGlobals.SystemStatus(GameData.Player.CurrentSystem) < 0)
                        {
                            MacToolbox.ForeColor(QuickDrawColor.Red);
                            MacToolbox.DrawString("Hostile");
                        }
                        else
                        {
                            MacToolbox.RGBForeColor(UiColorConstants.RestrictedNavWarning);
                            MacToolbox.DrawString("Forbidden");
                        }
                        MacToolbox.TextFace(0);
                    }
                }
                else
                {
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 42);
                    MacToolbox.RGBForeColor((uint)highlightColor);
                    MacToolbox.DrawString("Status: ");
                    MacToolbox.RGBForeColor((uint)UiColors.Neutral);
                    MacToolbox.DrawString("Dominated");
                }
            }
            else
            {
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 42);
                MacToolbox.RGBForeColor((uint)highlightColor);
                MacToolbox.DrawString("Status: ");
                MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
                MacToolbox.DrawString("Uninhabited");
            }
        }
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(dlg);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, dlg + 2,
                            MacToolbox.GetDialogPortRect(dlg), MacToolbox.GetDialogPortRect(dlg), 0, MacToolbox.GetDialogVisRgn(dlg));
    }
}
