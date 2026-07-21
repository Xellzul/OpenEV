using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Title;

// FUN_100451f0 — the Game Speed sub-dialog (DLOG 0xfa2), opened from
// PrefsDialogInit when the user clicks item 0x2a ("Game Speed…"). The
// dialog has OK (1) / Cancel (2) buttons and a drag-handle slider (item 4)
// that edits PrefsDialogState.GameSpeedPercent (0..175; +50 = the percent).
// Decompile: EV Override-11.c lines 28749-28859.
//
// State lives in PrefsDialogState (the old pointer cells + scratch, now
// managed fields). GetDialogItem's rect out (item 4) is a managed array; the
// type/handle outs are discarded since the original never reads them back.
//
// ModalDialog(0, …) means no filter — the slider redraw (DrawGameSpeedSlider)
// is registered as the DLOG 0xfa2 UserItem draw in PrefsMemory.Init so
// ModalDialog's background fill doesn't erase it.
public static class GameSpeedDialog
{
    public static void Run()
    {
        for (int i = 0; i < PrefsDialogState.GameSpeedPicts.Length; i++)
        {
            PrefsDialogState.GameSpeedPicts[i] = 0;
            PrefsDialogState.GameSpeedPicts[i] = MacToolbox.GetPicture(i + 400);
            if (PrefsDialogState.GameSpeedPicts[i] != 0)
            {
                MacToolbox.HNoPurge(PrefsDialogState.GameSpeedPicts[i]);
            }
        }
        PrefsDialogState.GameSpeedDialogWindow = 0;
        PrefsDialogState.GameSpeedDialogWindow = MacToolbox.GetNewDialog(4002, 0, -1);
        if (PrefsDialogState.GameSpeedDialogWindow != 0)
        {
            NewDialogHook.Run(PrefsDialogState.GameSpeedDialogWindow, 0);
            MacToolbox.ShowWindow(PrefsDialogState.GameSpeedDialogWindow);
            MacToolbox.SelectWindow(PrefsDialogState.GameSpeedDialogWindow);
            MacToolbox.SetPort(PrefsDialogState.GameSpeedDialogWindow);
            MacToolbox.DrawDialog(PrefsDialogState.GameSpeedDialogWindow);
            DrawDefaultButtonOutline.Run(PrefsDialogState.GameSpeedDialogWindow, 1);
            DrawGameSpeedSlider.Run();

            // The value on entry, restored if the user cancels.
            short savedSpeed = PrefsDialogState.GameSpeedPercent;
            bool done = false;
            short itemHit = default;
            do
            {
                MacToolbox.ModalDialog(0, ref itemHit);
                if (itemHit == 1)
                {
                    done = true;
                }
                if (itemHit == 2)
                {
                    PrefsDialogState.GameSpeedPercent = savedSpeed;
                    done = true;
                }
                if (itemHit == 4)   // slider area clicked
                {
                    if (MacToolbox.StillDown())
                    {
                        var itemRect = new short[4];
                        MacToolbox.GetDialogItem(PrefsDialogState.GameSpeedDialogWindow, 4, null, null, itemRect);
                        short rectLeft = itemRect[1];
                        short rectBottom = itemRect[2];
                        short rectRight = itemRect[3];

                        // PtInRect test rect: {top: bottom-18, left, bottom, right}. (The
                        // decompile also builds a second {bottom-14, left, bottom-3, right}
                        // rect at &local_28 — dead, never read.)
                        short[] hitRect = { (short)(rectBottom - 18), rectLeft, rectBottom, rectRight };
                        int mousePt = MacToolbox.GetMouse();
                        if (MacToolbox.PtInRect(mousePt, hitRect))
                        {
                            UpdateGameSpeedFromMouse(mousePt, rectLeft);
                            while (MacToolbox.StillDown())
                            {
                                UpdateGameSpeedFromMouse(MacToolbox.GetMouse(), rectLeft);
                            }
                        }
                    }
                }
            } while (!done);

            // Bug-for-bug: the release loop runs `< 1`, so only GameSpeedPicts[0]
            // (PICT 400, the track) is purged — PICT 401 (the thumb) leaks every
            // time the dialog opens.
            for (int i = 0; i < 1; i++)
            {
                if (PrefsDialogState.GameSpeedPicts[i] != 0)
                {
                    MacToolbox.HPurge(PrefsDialogState.GameSpeedPicts[i]);
                    MacToolbox.ReleaseResource(PrefsDialogState.GameSpeedPicts[i]);
                }
            }
            MacToolbox.DisposeDialog(PrefsDialogState.GameSpeedDialogWindow);
        }
    }

    // Slider drag: speed = mouse-h - rectLeft - 5 (5px inset from the track's
    // left edge), clamped 0..175. Shared body of decompile 28818-28826 /
    // 28831-28839 (the original repeats this verbatim for the initial hit and
    // every StillDown() iteration).
    private static void UpdateGameSpeedFromMouse(int mousePt, short rectLeft)
    {
        // mousePt is a packed Point {v hi, h lo}; (short)mousePt takes the h coordinate.
        PrefsDialogState.GameSpeedPercent = (short)((short)mousePt - rectLeft - 5);
        if (PrefsDialogState.GameSpeedPercent < 0) PrefsDialogState.GameSpeedPercent = 0;
        if (175 < PrefsDialogState.GameSpeedPercent) PrefsDialogState.GameSpeedPercent = 175;
        DrawGameSpeedSlider.Run();
        MacToolbox.SetPort(PrefsDialogState.GameSpeedDialogWindow);
    }
}
