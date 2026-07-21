using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Title;

// FUN_10045504 (EV Override-11.c lines 28860-28926) — draws the Game Speed
// sub-dialog slider: the track (PICT 400) and thumb (PICT 401, positioned by
// the speed value), the "Game Speed:" label and the "<percent>%" readout,
// composed into the BACKDROP offscreen then CopyBits to the dialog — the
// same offscreen-compose route the prefs keys-grid uses.
public static class DrawGameSpeedSlider
{
    private const string SliderLabel = "Game Speed:";  // Pascal 0b "Game Speed:" at 0x100844b3
    private const string PercentSuffix = "%";            // Pascal 01 "%" at 0x100820ac

    public static void Run()
    {
        // Decompile targets the Set Prefs dialog window here, not the Game
        // Speed dialog — harmless, since SetPortAndDevice below immediately
        // retargets the port to the BACKDROP offscreen; kept faithfully.
        MacToolbox.SetPort(PrefsDialogState.DialogWindow);
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);

        var itemRect = new short[4];
        MacToolbox.GetDialogItem(PrefsDialogState.GameSpeedDialogWindow, 4, null, null, itemRect);
        short left = itemRect[1];
        short bottom = itemRect[2];
        short right = itemRect[3];
        MacToolbox.ForeColor(QuickDrawColor.White);
        MacToolbox.PaintRect(itemRect);   // erase the slider area
        MacToolbox.ForeColor(QuickDrawColor.Black);

        // Decompile re-reads this global twice; cached once here (nothing
        // writes it in between).
        short speed = PrefsDialogState.GameSpeedPercent;

        // Track rect = {bottom-11, left, bottom-6, right}. The last field is
        // itemRect's RIGHT, not bottom — verified against the ASM's CONCAT22
        // register packing.
        var track = new short[4];
        track[0] = (short)(bottom - 11);
        track[1] = left;
        track[2] = (short)(bottom - 6);
        track[3] = right;
        if (PrefsDialogState.GameSpeedPicts[0] == 0)
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.FrameRect(track);
        }
        else
        {
            MacToolbox.DrawPicture(PrefsDialogState.GameSpeedPicts[0], track);
        }

        // Thumb rect at x = left + speed (9px knob).
        short thumbX = (short)(left + speed);
        var thumb = new short[4];
        thumb[0] = (short)(bottom - 17);
        thumb[1] = thumbX;
        thumb[2] = bottom;
        thumb[3] = (short)(thumbX + 9);
        if (PrefsDialogState.GameSpeedPicts[1] == 0)
        {
            MacToolbox.ForeColor(QuickDrawColor.Blue);
            MacToolbox.PaintRect(thumb);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        else
        {
            MacToolbox.DrawPicture(PrefsDialogState.GameSpeedPicts[1], thumb);
        }

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.MoveTo(left, bottom - 25);
        MacToolbox.DrawString(SliderLabel);
        string numStr = (speed + 50).ToString();   // NumToString(percent)
        int suffixWidth = MacToolbox.StringWidth(PercentSuffix);
        int numStrWidth = MacToolbox.StringWidth(numStr);
        MacToolbox.MoveTo(right - (numStrWidth + suffixWidth), bottom - 25);
        MacToolbox.DrawString(numStr);
        MacToolbox.DrawString(PercentSuffix);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(PrefsDialogState.GameSpeedDialogWindow);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, PrefsDialogState.GameSpeedDialogWindow + 2,
                             itemRect, itemRect, 0, 0);
    }
}
