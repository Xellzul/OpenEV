using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000efb8 (EV Override-11.c lines 7863-7903) — draw the BAR dialog's
// 6-button row ("RenderMissionBbsButtonRow" was an early transcription misname). Button i
// draws into the rect of DITL item i+1 from the {normal, pressed} PICT pairs in
// DialogScratch.SpaceportPicts; activeButton draws pressed art (-1 = none).
// Button 3 is ALWAYS disabled (painted out — a shipped-game dead button);
// button 4 (hire escort) is gated on EscortRoomAvailable.
public static class DrawBarButtonRow
{
    // Bar dialog's button-row item count (DITL items 1..6); shared with TrackBarButtonHit.
    internal const int ButtonCount = 6;

    public static void Run(short activeButton)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var btnRects = new short[ButtonCount][];   // auStack_52: 6 contiguous stack Rects
        for (int i = 0; i < ButtonCount; i++) btnRects[i] = new short[4];

        int window = DialogScratch.SpaceportDialogRecord;
        for (int i = 0; i < ButtonCount; i++)
        {
            MacToolbox.GetDialogItem(window, i + 1, itemType, itemHandle, btnRects[i]);
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (short btn = 0; btn < ButtonCount; btn = (short)(btn + 1))
        {
            bool enabled = true;
            if (btn == 4)
            {
                enabled = EscortRoomAvailable.Run();
            }
            if (btn == 3)
            {
                enabled = false;
            }
            if (!enabled)
            {
                MacToolbox.PaintRect(btnRects[btn]);
            }
            else if (activeButton == btn)
            {
                // pressed pict: SpaceportPicts[btn*2+1] (the {normal, pressed} pair's odd slot)
                MacToolbox.DrawPicture(DialogScratch.SpaceportPicts[btn * 2 + 1], btnRects[btn]);
            }
            else
            {
                MacToolbox.DrawPicture(DialogScratch.SpaceportPicts[btn * 2], btnRects[btn]);
            }
        }
    }
}
