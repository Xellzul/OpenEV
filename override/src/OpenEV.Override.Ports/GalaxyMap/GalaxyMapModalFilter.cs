using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.GalaxyMap;

// Port of FUN_10034420 (EV Override-11.c 21390-21560) — the modal filter for the
// galaxy-map dialog (DLOG 2000). Re-derives the +/- zoom-button enables from the live
// zoom each event; Return/Enter = done (item 1); Tab/'\' cycles the hyper-route target
// through the current system's shown links; 'c' recentres; '-'/'+'/'=' fire zoom items
// 4/5; arrows pan by 10; map-area clicks report item 3, other clicks track the button row.
public static class GalaxyMapModalFilter
{
    // Cycle limit is 5 in the original: only the first 5 of a system's 16 hyperlinks are
    // Tab-cycleable (availability, though, is scanned across all 16 links).
    private const int MaxNavTargets = 5;

    // The `dialog` parameter (the ModalFilterProc's theDialog) is intentionally unused:
    // like the original, every dialog call reads the stored map DialogPtr global
    // (GalaxyMapState.MapDialog = *_DAT_10080cbc), not the passed-in filter dialog. The
    // param is kept only to match the Mac ModalFilterProc signature the bridge calls.
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        if (GalaxyMapState.Zoom <= GalaxyMapGlobals.ZoomDetailFarThreshold)
            GalaxyMapState.MinusEnabled = 0;
        else
            GalaxyMapState.MinusEnabled = 1;

        if (GalaxyMapGlobals.ZoomMaxThreshold <= GalaxyMapState.Zoom)
            GalaxyMapState.PlusEnabled = 0;
        else
            GalaxyMapState.PlusEnabled = 1;

        GalaxyMapState.ResetFlag = 0;

        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
            if (keyChar == '\r' || keyChar == '\x03')   // Return / Enter
            {
                itemHit = 1;
                return 1;
            }
            if ((keyChar == '\t' || keyChar == '\\') && GalaxyMapState.TradeKeyLock == 0)
            {
                var player = GameData.Player;
                player.NavMode = 3;
                short availableCount = 0;
                var currentSystem = GameData.Systems[player.CurrentSystem];
                byte[] available = new byte[SystRecord.HyperLinkCount];
                for (short loopIndex = 0; loopIndex < SystRecord.HyperLinkCount; loopIndex++)
                {
                    available[loopIndex] = 0;
                    short link = currentSystem.HyperLink[loopIndex];
                    if (link != -1 && GameData.Systems[link].ShownFlag != 0)
                    {
                        available[loopIndex] = 1;
                        availableCount++;
                    }
                }
                if (availableCount < 1)
                {
                    player.NavTargetSpob = -1;
                }
                else
                {
                    do
                    {
                        player.NavTargetSpob = (short)(player.NavTargetSpob + 1);
                        if (MaxNavTargets - 1 < player.NavTargetSpob)
                            player.NavTargetSpob = 0;
                        // Original guard, kept for parity: never triggers on this function's
                        // increment-only path (NavTargetSpob is only -1/0/4/++ here, so post-++
                        // it is never negative).
                        if (player.NavTargetSpob < 0)
                            player.NavTargetSpob = MaxNavTargets - 1;
                    } while (available[player.NavTargetSpob] == 0);
                }
                WorldState.SpawnPulseDirty = 1;
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 2, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 6, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                TickHudRedrawScheduler.Run();
                MacToolbox.SetPort(GalaxyMapState.MapDialog);

                if (player.NavMode == 3 && player.NavTargetSpob != -1)
                {
                    GalaxyMapState.CentredSystem = currentSystem.HyperLink[player.NavTargetSpob];
                }
                else
                {
                    GalaxyMapState.CentredSystem = player.CurrentSystem;
                }
            }
            if (keyChar == 'c')
            {
                var player = GameData.Player;
                var currentSystem = GameData.Systems[player.CurrentSystem];
                SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                WorldState.MapViewCentreX = currentSystem.XPos;
                WorldState.MapViewCentreY = currentSystem.YPos;
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 2, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 6, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
            }
            if (keyChar == '-')
            {
                itemHit = 4;
                return 1;
            }
            if (keyChar == '+' || keyChar == '=')
            {
                itemHit = 5;
                return 1;
            }
            if (keyChar == '\x1c')   // left arrow
            {
                WorldState.MapViewCentreX = (short)(WorldState.MapViewCentreX - 10);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
            }
            if (keyChar == '\x1d')   // right arrow
            {
                WorldState.MapViewCentreX = (short)(WorldState.MapViewCentreX + 10);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
            }
            if (keyChar == '\x1e')   // up arrow
            {
                WorldState.MapViewCentreY = (short)(WorldState.MapViewCentreY - 10);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
            }
            if (keyChar == '\x1f')   // down arrow
            {
                WorldState.MapViewCentreY = (short)(WorldState.MapViewCentreY + 10);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
            }
        }

        if (evt.WhatType == MacEventType.MouseDown)
        {
            int localPoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, itemType, itemHandle, itemRect);
            if (!MacToolbox.PtInRect(localPoint, itemRect))
            {
                short buttonHit = (short)Track4ButtonMouseDown.Run(localPoint);
                if (buttonHit == 0)
                {
                    itemHit = 1;
                    return 1;
                }
                else if (buttonHit == 1)
                {
                    itemHit = 4;
                    return 1;
                }
                else if (buttonHit == 2)
                {
                    itemHit = 5;
                    return 1;
                }
                else if (buttonHit == 3)
                {
                    itemHit = 8;
                    return 1;
                }
                else
                {
                    itemHit = -1;
                    return 1;
                }
            }
            else
            {
                itemHit = 3;
                return 1;
            }
        }
        else
        {
            if (evt.WhatType == MacEventType.UpdateEvt)
            {
                MacToolbox.BeginUpdate(GalaxyMapState.MapDialog);
                DrawGalaxyMap.Run();
                MacToolbox.EndUpdate(GalaxyMapState.MapDialog);
            }
            return 0;
        }
    }
}
