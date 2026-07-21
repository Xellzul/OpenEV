using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_10042864 (EV Override-11.c lines 27530-27584): the endgame victory
// animation — draw PICT 0x1f4b centred on the backdrop, then a 10-frame
// vertical reveal (the rect band grows 19px/frame) blitted from the backdrop
// GWorld to the screen pixmap, wait for a click, play the UI chime, clean up.
public static class PlayVictoryAnimation
{
    public static void Run()
    {
        SndPlay.Run(SoundResourceCells.DeathCountdownSnd, 1, 128, 128);
        int picture = MacToolbox.GetPicture(0x1f4b);
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);   // FUN_1007aa68

        var port = GlobalState.PortRect;
        short[] srcRect = { port[0], port[1], port[2], port[3] };
        RectCenter.Run(picture, srcRect);
        MacToolbox.DrawPicture(picture, srcRect);
        SetGamePortAndDevice.Run();

        MacToolbox.InsetRect(srcRect, 0, 190);
        short[] dstRect = { srcRect[0], srcRect[1], srcRect[2], srcRect[3] };
        for (short frame = 0; frame < 10; frame = (short)(frame + 1))
        {
            int frameStartTicks = (int)MacToolbox.TickCount();
            MacToolbox.InsetRect(dstRect, 0, -19);   // grow the reveal band 19px/frame
            MacToolbox.InsetRect(srcRect, 0, -19);
            MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2,
                                srcRect, dstRect, 0, 0);
            uint nowTicks;
            do
            {
                nowTicks = MacToolbox.TickCount();
            } while (nowTicks < (uint)(frameStartTicks + 1));
        }
        while (!MacToolbox.Button()) { }
        // TOC+0x1f60 = 0x1008a5c0 = UiSoundBankA[0], not an uninitialized local.
        SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
        MacToolbox.HPurge(picture);
        MacToolbox.ReleaseResource(picture);
        SetGamePortAndDevice.Run();
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
        MacToolbox.InvalRect(dstRect);
    }
}
