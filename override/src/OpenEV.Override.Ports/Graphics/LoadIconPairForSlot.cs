using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1001e4fc (EV Override-11.c lines 13531-13593): claim the next sprite GWorld
// slot, create its offscreen colour GWorld over {0, 0, cellHeight*rows, cellWidth*cols}
// (DecodePictResource), draw PICT pictId1 into it, build the matching MASK GWorld
// (AllocateGWorldPortStruct) and draw PICT pictId2 INVERTED into that. Any failure is
// fatal (missing graphics).
//
// The two per-slot GWorld record tables (stride 0x1a) are the managed SlotGWorlds.Sprite/
// Mask records now. RegisterDiscardTarget is a host
// bridge: the slot GWorld is staging-only, so its draws are discarded rather than falling
// onto the visible screen.
public static class LoadIconPairForSlot
{
    public static void Run(short pictId1, short pictId2, short cellWidth, short cellHeight,
                           short cols, short rows)
    {
        short boundsBottom = (short)(cellHeight * rows);
        short boundsRight = (short)(cellWidth * cols);
        int boundsBotRightPacked = (boundsBottom << 16) | (boundsRight & 0xffff);
        short[] bounds = { 0, 0, boundsBottom, boundsRight };

        RenderGlobals.SpriteLoadSlotIndexSaved = RenderGlobals.SpriteLoadSlotIndex;
        bool slotOverflow = 0xff < RenderGlobals.SpriteLoadSlotIndex;   // slot index overflows a byte
        RenderGlobals.SpriteLoadSlotIndex = (short)(RenderGlobals.SpriteLoadSlotIndex + 1);
        if (slotOverflow)
            Misc.FatalGraphicsResourceExit.Run();

        SlotGWorldRecord spriteRec = SlotGWorlds.Sprite[RenderGlobals.SpriteLoadSlotIndexSaved];
        short status = (short)DecodePictResource.Run(spriteRec, 0, boundsBotRightPacked);
        if (status != 0)
            Misc.FatalGraphicsResourceExit.Run();
        MacToolbox.RegisterDiscardTarget(spriteRec.Port + 2);

        int picHandle = MacToolbox.GetPicture(pictId1);
        if (picHandle == 0)
            Misc.FatalGraphicsResourceExit.Run();
        SetPortAndDevice.Run(spriteRec.Port, spriteRec.GDevice);
        MacToolbox.DrawPicture(picHandle, bounds);
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.ReleaseResource(picHandle);

        SlotGWorldRecord maskRec = SlotGWorlds.Mask[RenderGlobals.SpriteLoadSlotIndexSaved];
        AllocateGWorldPortStruct.Run(maskRec, 0, boundsBotRightPacked);
        MacToolbox.RegisterDiscardTarget(maskRec.Port + 2);
        picHandle = MacToolbox.GetPicture(pictId2);
        if (picHandle == 0)
            Misc.FatalGraphicsResourceExit.Run();
        // mask GDevice = 0 (B&W) → SetGDevice skipped
        SetPortAndDevice.Run(maskRec.Port, maskRec.GDevice);
        MacToolbox.DrawPicture(picHandle, bounds);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.InvertRect(bounds[0], bounds[1], bounds[2], bounds[3]);
        SetGamePortAndDevice.Run();
        MacToolbox.ReleaseResource(picHandle);
    }
}
