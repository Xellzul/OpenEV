using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1000bb44 (EV Override-11.c 6097-6127) — draws the three spinning reels of
// the spaceport "slot machine" (the bribe dialog, DLOG 0x3f7, reused as a
// one-armed-bandit minigame). For each of the 3 dialog items it copies a 64×64
// cell out of the backdrop GWorld at that reel's current scroll offset, frames
// it, then blits the framed cell into the dialog item.
public static class DrawSlotMachineReels
{
    private const short CellSize = 64;
    private const short DstXOffset = 128; // dst cell sits 128px (two cells) right of the src cell
    private const short FrameColor = 30;  // QuickDraw colour index — cell frame
    private const short RestoreColor = 33;  // QuickDraw colour index — restored after frame

    public static void Run()
    {
        var itemRect = new short[4]; // GetDialogItem rect out + final CopyBits dst
        var srcRect = new short[4]; // reel cell source rect in the backdrop
        var dstRect = new short[4]; // framed copy, 128px (two cells) to the right

        int dialog = DialogScratch.BribeDialogPtr;
        int backdrop = RenderGlobals.BackdropGWorld;

        for (short reel = 0; reel < 3; reel++)
        {
            MacToolbox.GetDialogItem(dialog, reel + 1, null, null, itemRect);

            short cellY = DialogScratch.CommFaceX[reel]; // reel scroll offset
            MacToolbox.SetRect(srcRect, 0, cellY, CellSize, (short)(cellY + CellSize));

            dstRect[0] = srcRect[0]; dstRect[1] = srcRect[1];
            dstRect[2] = srcRect[2]; dstRect[3] = srcRect[3];

            SetPortAndDevice.Run(backdrop, 0);
            MacToolbox.OffsetRect(dstRect, DstXOffset, 0);
            MacToolbox.CopyBits(backdrop + 2, backdrop + 2, srcRect, dstRect, 0, 0);
            MacToolbox.ForeColor(FrameColor);
            MacToolbox.FrameRect(dstRect);
            MacToolbox.ForeColor(RestoreColor);
            SetGamePortAndDevice.Run();

            MacToolbox.SetPort(dialog);
            MacToolbox.CopyBits(backdrop + 2, dialog + 2, dstRect, itemRect, 0, 0);
        }
    }
}
