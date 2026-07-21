using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007ab8c (EV Override-11.c lines 52513-52553).
//
// Stages the in-game backdrop PICT: stores the scroll/pict ids into the render context, then
// (unless pictIdB == 0, as with the boot's SetScrollViewPosition(0,0)) loads the PICT resource
// and draws it into the anim-scratch GWorld (mode 1 or 2 by the sprite-loop flag) over the
// {0,0,InnerRight,InnerBottom} stage rect, then CopyBits anim-scratch -> offscreen-game over the
// game port's portRect.
//
// MANAGED: the render context is GlobalState (the original read the 0x10080d08
// record); the save/restore pair and the stage Rect are values, not MacScratch.
public static class LoadAndStagePictResource
{
    public static void Run(int pictIdA, int pictIdB)
    {
        SaveCurrentPortAndDevice.Run(out int savedPort, out int savedDevice);
        GlobalState.ScrollHoriz = (short)pictIdA;
        GlobalState.ScrollVert = (short)pictIdB;
        // Decompile bit-twiddle (52527-52530): under Color QD at depth > 1-bit, a non-zero
        // pictIdA overrides pictIdB. The ASM ANDs in ColorQuickDrawFlag's raw byte and tests only
        // its low bit, not the whole byte "!= 0" — `& 1` matches that exactly.
        if ((short)pictIdA != 0 && (GlobalState.ColorQuickDrawFlag & 1) != 0 && GlobalState.RenderMode > 1)
        {
            pictIdB = pictIdA;
        }
        if ((short)pictIdB != 0)
        {
            GWorldPort.SetActivePortScratch();
            MacToolbox.SetResLoad(false);
            int handle = MacToolbox.GetResource(MacResType.Pict, pictIdB);
            MacToolbox.SetResLoad(true);
            if (handle == 0)
                // Message from data-seg cell 0x10085b4c (StaticData.UiErrorStrings[BackdropLoadFailedIndex]).
                FatalOutOfMemoryExit.Run(StaticData.UiErrorStrings[StaticData.BackdropLoadFailedIndex]);

            // Stage rect = {0, 0, InnerRight, InnerBottom} (SetRect(&local, 0, 0, InnerRight, InnerBottom)).
            int stageTopLeft = 0;
            int stageBotRight = (GlobalState.InnerBottom << 16) | (GlobalState.InnerRight & 0xffff);
            GWorldPort.SetActivePortScratch();
            if (GlobalState.SpriteLoopEnabled == 0)
                LoadPictBlit_Mode2.Run((short)pictIdB, stageTopLeft, stageBotRight);
            else
                LoadPictBlit_Mode1.Run((short)pictIdB, stageTopLeft, stageBotRight);

            GWorldPort.SetActivePortSecondaryGame();
            // CopyBits anim-scratch -> offscreen-game, using the OFFSCREEN port's portRect for
            // both src and dst rects (original quirk, preserved: not the scratch port's own rect).
            short[] portRect = MacToolbox.GetPortRectShorts(GlobalState.OffscreenGameGWorld);
            MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.OffscreenGameGWorld + 2,
                                portRect, portRect, 0, 0);
        }
        SetPortAndDevice.Run(savedPort, savedDevice);
    }
}
