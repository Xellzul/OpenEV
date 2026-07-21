using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_100120cc (EV Override-11.c lines 9155-9216; ASM: reference/disasm/
// _code_interstitial.asm loc_120CC — no top-level `sub_`, reached only via
// the filter-proc pointer table off_82490) — the modal filter of the
// planet DOMINATION/tribute comm dialog (ShowSpobHailDialog,
// DLOG 0x3f1; the "TradeDockRefuel" class name is an early transcription misname kept for the
// file/FUN pairing). Keys: Return/Enter/'e' = leave (item 1); 'o'/'b' = obey/
// bribe while hostile, 'g' = greet while welcome (item 2); 'd'/'t' = dominate/
// tribute while not yet dominated, 'r' = release once dominated (item 3).
// Mouse-downs map the three comm button zones (TrackPlanetCommButtonRow) onto items
// 1..3; update events redraw via DrawShipInfoPanel. Registered under
// Dialog.Model.DialogScratch.DominateFilterProc.
//
// Dialog 4-rules rewrite: typed MacEvent filter over the real EventRecord
// offsets — the key branch's charCode is the low byte of message@+2
// (decompile 9173 `FUN_100760fc((int)(char)*(undefined4 *)(param_2 + 1))`,
// evt.Message). The updateEvt EndUpdate reads `*(toc-0x1ba0)` (cell
// 0x10086ac0) via what the decompile renders as a zero local (a Pass-1
// mis-rendering of a TOC-relative access, not a real near-null read) — it is
// the same SpaceportCommDialogRecord cell BeginUpdate uses.
public static class TradeDockRefuelDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int result;

        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
            if (keyChar == '\r' || keyChar == '\x03' || keyChar == 'e')
            {
                itemHit = 1;
                return 1;
            }
            if ((keyChar == 'o' || keyChar == 'b') && DialogScratch.CommHailGateFlag != 0)
            {
                itemHit = 2;
                return 1;
            }
            if (keyChar == 'g' && DialogScratch.CommHailGateFlag == 0)
            {
                itemHit = 2;
                return 1;
            }
            if ((keyChar == 'd' || keyChar == 't') &&
               Core.Model.GameData.Spobs[Core.Model.GameData.Player.NavTargetSpob].TradingEnabled == 0)
            {
                itemHit = 3;
                return 1;
            }
            if (keyChar == 'r' &&
               Core.Model.GameData.Spobs[Core.Model.GameData.Player.NavTargetSpob].TradingEnabled != 0)
            {
                itemHit = 3;
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            int localPoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short regionIndex = (short)TrackPlanetCommButtonRow.Run(localPoint);
            if (regionIndex < 0 || 2 < regionIndex)
            {
                itemHit = -1;
                result = 1;
            }
            else
            {
                itemHit = (short)(regionIndex + 1);
                result = 1;
            }
        }
        else
        {
            if (evt.WhatType == MacEventType.UpdateEvt)
            {
                MacToolbox.BeginUpdate(DialogScratch.SpaceportCommDialogRecord);
                DrawShipInfoPanel.Run();
                MacToolbox.EndUpdate(DialogScratch.SpaceportCommDialogRecord);
            }
            result = 0;
        }
        return result;
    }
}
