using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10044ef4 — redraws the Set Prefs keybind grid: PICT 132 ("Keys")
// backdrop (item 0x22) + the 31 slot rects (items 3..0x21) with their
// current keycode labels — into the BACKDROP offscreen GWorld, then
// CopyBits the keys region onto the dialog. EV Override-11.c lines
// 28663-28748. The caller (the modal filter) follows with DrawDialog for
// the standard items.
public static class PrefsDialogDraw
{
    // Fallback label when STR# 129 has no name for the keycode — Pascal
    // string at &DAT_100844ab in the PEF data segment (dumped bytes:
    // 07 "Unknown").
    private const string UnknownKeyLabel = "Unknown";

    // DITL 4001 keybind-slot items: item N binds keymap slot N-FirstSlotItem.
    private const int FirstSlotItem = 3;
    private const int LastSlotItem = FirstSlotItem + Keymap.LiveCount - 1;   // 33

    public static void Run()
    {
        var itemHandle = new int[1];
        var picRect = new short[4];   // item 34 rect, also the final CopyBits region
        var slotRect = new short[4];
        var slotRectInset = new short[4];

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);             // SetPort → BACKDROP offscreen
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.TextFont(0);
        MacToolbox.TextSize(12);
        MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 34, null, itemHandle, picRect);
        MacToolbox.DrawPicture(PrefsDialogState.Pict132Handle, picRect);
        for (int slotItem = FirstSlotItem; (short)slotItem <= LastSlotItem; slotItem++)
        {
            short slotItemShort = (short)slotItem;
            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, slotItem, null, null, slotRect);
            bool slotVisible = MacToolbox.RectInRgn(slotRect, MacToolbox.GetDialogVisRgn(PrefsDialogState.DialogWindow));
            if (slotVisible)
            {
                if (PrefsDialogState.SelectedKeybindSlot + FirstSlotItem == (int)slotItemShort)
                {
                    System.Array.Copy(slotRect, slotRectInset, 4);
                    MacToolbox.InsetRect(slotRectInset, 1, 1);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.FrameRect(slotRect);
                    MacToolbox.InsetRect(slotRectInset, 1, 1);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.PaintRect(slotRectInset);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.FrameRect(slotRect);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.MoveTo(slotRect[1] + 5, slotRect[0] + 15);
                }
                else
                {
                    System.Array.Copy(slotRect, slotRectInset, 4);
                    MacToolbox.InsetRect(slotRectInset, 1, 1);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.PaintRect(slotRectInset);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.FrameRect(slotRect);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.MoveTo(slotRect[1] + 5, slotRect[0] + 15);
                }
                string keyLabel = MacToolbox.GetIndString(129, (short)(Keymap.LiveGet(slotItemShort - FirstSlotItem) + 1));
                if (keyLabel.Length == 0)
                {
                    MacToolbox.DrawString(UnknownKeyLabel);
                }
                else
                {
                    MacToolbox.DrawString(keyLabel);
                }
            }
        }
        // Volume readout: SetDialogItemText's the Str255 at VolumeLabels[DialogWorkingVolume].
        MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 37, null, itemHandle, slotRect);
        MacToolbox.SetDialogItemText(itemHandle[0],
            PrefsDialogState.VolumeLabels[Core.Model.GamePrefs.DialogWorkingVolume]);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(PrefsDialogState.DialogWindow);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // Decompile `*(toc+0x708c)+2` resolves via GameToc to ReadInt(0x1008f6ec)+2 — the BACKDROP
        // offscreen GWorld this function SetPort'd at the top (same pattern as DrawGameSpeedSlider /
        // RedrawAboutDialog).
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, PrefsDialogState.DialogWindow + 2,
                            picRect, picRect, 0, MacToolbox.GetDialogVisRgn(PrefsDialogState.DialogWindow));
    }
}
