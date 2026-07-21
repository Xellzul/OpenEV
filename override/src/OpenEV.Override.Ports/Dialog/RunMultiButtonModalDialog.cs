using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10077460 (EV Override-11.c lines 50491-50691) — the generic N-button modal
// alert (FatalOutOfMemoryExit's "Quit" alert etc.). Synthesizes a DITL from the
// InitUiSfxConfig template (one ^0^1^2^3 statText + up to 3 buttons), NewDialogs
// a (0x73,0x50)-(0x163,0xdc) window centred on the main device, ParamTexts the
// four message strings, titles the buttons (default-button ring on the matching
// one via SetDialogItemTitleAndMaybeOutline) and runs ModalDialog under
// StandardDialogFilter (Return/Enter = default button, cmd-period = cancel).
// Returns the 0-based button index (itemHit 1 = the statText maps to the
// default button; buttons are items 2..4).
//
// The decompile's seven 0x100-byte Pascal copy loops (auStack_X3e header +
// local_X36 body stack overlays) collapse into the string parameters below;
// ParamText takes the managed-string funnel, and the button titles flow into
// SetDialogItemTitleAndMaybeOutline.
public static class RunMultiButtonModalDialog
{
    // Port bridge for the modal-filter UPP (registered below, see
    // NewRoutineDescriptor). The filter's item-hit out lands in _filterHit;
    // the core ModalDialog loop reads only the consumed flag.
    private static short _filterHit;
    private static int FilterAdapter(int dialog, MacEvent evt)
        => StandardDialogFilter.Run(dialog, evt, ref _filterHit);

    // Original arg list is 10-wide: (text0..text3 = ParamText ^0..^3, buttonCount,
    // defaultButton, cancelButton, btn1Title = param_8, btn2Title = in_stack_38,
    // btn3Title = in_stack_3c). The decompile dropped the last two off the signature;
    // optional-arg defaults cover callers that omit unused button titles
    // (FatalOutOfMemoryExit passes only btn1Title = "Quit").
    public static int Run(string text0, string text1, string text2, string text3,
                          short buttonCount, short defaultButton, short cancelButton,
                          string btn1Title = "", string btn2Title = "", string btn3Title = "")
    {
        var savedPort = new int[1];   // local_740 — GetPort save (GetPort/SetPort(savedPort[0]) restore idiom)
        var dlgRect = new short[4];   // local_36/_34/_32/_30 — the dialog bounds Rect
        short itemHit = 0;            // local_2e[9] — only [0] is ever used

        // Publish the default/cancel button indices for StandardDialogFilter to
        // read (see its own field comments for the original cell provenance).
        StandardDialogFilter.DefaultButtonIndex = defaultButton;
        StandardDialogFilter.CancelButtonIndex = cancelButton;
        InitUiSfxConfig.Run();   // FUN_10077210 — (re)build the synthesized multi-button DITL template
        if (2 < buttonCount)
        {
            // Shrinks the statText Rect.bottom to 3px above the third button
            // row's Rect.top (see InitUiSfxConfig for the full DITL layout).
            InitUiSfxConfig.MultiButtonDitlTemplate[5] =
                (short)(InitUiSfxConfig.MultiButtonDitlTemplate[0x1c] - 3);
        }
        MacToolbox.InitCursor();
        MacToolbox.GetPort(savedPort);
        int savedGDevice = MacToolbox.GetGDevice();   // local_73c
        MacToolbox.GetMainDevice();
        MacToolbox.SetGDevice();
        InitUiSfxConfig.MultiButtonDitlTemplate[0] = buttonCount;   // itemCount-1
        // The Mac NewHandle(0x200)'d a block, BlockMoved the synthesized DITL into
        // it (0x200 bytes from the 0x42-byte template — an over-read of adjacent
        // zero BSS) and passed it as NewDialog's items list, never disposing the
        // handle (leaked) — see the NO-OP note below for the port's disposition.
        // Build the window bounds: SetRect(0x73,0x50,0x163,0xdc), normalize to (0,0),
        // then centre on the main device gdRect. The decompile's `>>1` + negative-odd
        // carry pairs are signed round-toward-zero halving = C# integer `/ 2`.
        MacToolbox.SetRect(dlgRect, 115, 80, 355, 220);
        MacToolbox.OffsetRect(dlgRect, (short)-dlgRect[1], (short)-dlgRect[0]);
        MacToolbox.GetMainDeviceBounds(out short gdTop, out short gdLeft, out short gdBottom, out short gdRight);
        // Horizontal: (gdRect.left + gdRect.right)/2 - width/2   (gd+0x24 / gd+0x28).
        short hSpan = (short)(gdRight + gdLeft);
        short hExtent = (short)(dlgRect[3] - dlgRect[1]);
        MacToolbox.OffsetRect(dlgRect, (short)(hSpan / 2 - hExtent / 2), 0);
        // Vertical: (gdRect.top + gdRect.bottom - 20)/2 + (20 - height/2)   (gd+0x22 / gd+0x26).
        short vSpan = (short)(gdBottom - 20 + gdTop);
        short vExtent = (short)(dlgRect[2] - dlgRect[0]);
        MacToolbox.OffsetRect(dlgRect, 0, (short)((20 - vExtent / 2) + vSpan / 2));
        // NewDialog(0, &bounds, title, visible=0, procID=1, behind=-1, goAway=0,
        // refCon=0 [, items=the DITL handle — dropped by the decompile]). Title arg = toc-0x6202
        // = ADDRESS 0x1008245e, an all-zero BSS byte = the empty Pascal title ("").
        // NO-OP: the port's NewDialog is still a params-absorber stub returning 0 —
        // programmatic (resource-less) dialogs aren't served by the managed Dialog Manager
        // yet, so this modal currently falls into the FatalAlertExit path below. Not hidden —
        // the only live caller is the fatal out-of-memory exit, which ExitToShells either way.
        int dialog = MacToolbox.NewDialog(0, dlgRect, "", 0, 1, -1, 0, 0);
        if (dialog == 0)
        {
            // ASM: caller passes r11 = its own r1 so FatalAlertExit can restore the
            // GrafPtr/GDevice saved above (GetPort/GetGDevice) before the risky call.
            FatalAlertExit.Run(savedPort[0], savedGDevice);
        }
        MacToolbox.ParamText(text0, text1, text2, text3);
        MacToolbox.SetPort(dialog);
        MacToolbox.ShowWindow(dialog);
        // Title the buttons; outline arg `((uint)(byte)((cond) << 1) << 0x1c) >> 0x1d`
        // collapses to cond ? 1 : 0 (draw the default-button ring on the match).
        if (buttonCount == 2)
        {
            SetDialogItemTitleAndMaybeOutline.Run(dialog, 3, btn2Title, (byte)(defaultButton == 2 ? 1 : 0));
            SetDialogItemTitleAndMaybeOutline.Run(dialog, 2, btn1Title, (byte)(defaultButton == 1 ? 1 : 0));
        }
        else if (buttonCount < 2)
        {
            if (0 < buttonCount)
            {
                SetDialogItemTitleAndMaybeOutline.Run(dialog, 2, btn1Title, (byte)(defaultButton == 1 ? 1 : 0));
            }
        }
        else if (buttonCount < 4)
        {
            SetDialogItemTitleAndMaybeOutline.Run(dialog, 4, btn3Title, (byte)(defaultButton == 3 ? 1 : 0));
            SetDialogItemTitleAndMaybeOutline.Run(dialog, 3, btn2Title, (byte)(defaultButton == 2 ? 1 : 0));
            SetDialogItemTitleAndMaybeOutline.Run(dialog, 2, btn1Title, (byte)(defaultButton == 1 ? 1 : 0));
        }
        // NewRoutineDescriptor(*(toc-0x6bd4), 0xfd0, 1): cell 0x10081a8c holds the PEF
        // TVector of FUN_100770ac = StandardDialogFilter — proc-key + typed
        // RegisterModalFilter per the established dialog pattern.
        int filterUpp = MacToolbox.NewRoutineDescriptor(StandardDialogFilter.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(StandardDialogFilter.FilterProc, FilterAdapter);
        MacToolbox.ModalDialog(filterUpp, ref itemHit);
        MacToolbox.DisposeRoutineDescriptor(filterUpp);
        // Map itemHit -> 0-based button index: item 1 (the statText, also what the
        // no-dialog fallback reports) = the default button; items 2..4 = buttons 1..3.
        short pressed = defaultButton;
        if (itemHit != 1)
        {
            pressed = (short)(itemHit + -1);
        }
        itemHit = pressed;
        // NO-OP: HUnlock(ditlHandle) fell with the write-only DITL heap staging above.
        MacToolbox.DisposeDialog(dialog);
        // Mac restore: SetPort(savedPort VALUE) + SetGDevice.
        MacToolbox.SetPort(savedPort[0]);
        MacToolbox.SetGDevice(savedGDevice);
        return itemHit;
    }
}
