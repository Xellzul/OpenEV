using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1001fe88 (EV Override-11.c lines 14200-14287). Per-frame updater for a
// streak-trail sprite node: steps the frame counter, picks the StreakFrames cell by
// streak type (UpdaterFlag), and reprojects screen X/Y from the camera centre + spawn coords.
public static class TickStreakSprite
{
    // StreakFrames is 8 streak rows x 8 ints (decompile byte-stride 0x20 / 4).
    private const int StreakFramesPerRow = 8;

    // Truncating divide-by-2 (rounds toward zero, == value / 2 in C#): the (value & 1)
    // low-bit test adds the round-up for negative-odd values. Keep the mask & 1.
    private static int HalfTowardZero(int value)
        => (value >> 1) + ((value < 0 && (value & 1) != 0) ? 1 : 0);

    public static void Run(int sprite)
    {
        var n = SpriteNodes.At(sprite);
        if (n.UpdaterPayload < 0 || WorldState.ClearStreaksFlag != 0)
        {
            EvoGlobals.ActiveStreakCount--;
            n.SpritePtr = 0;
            n.UpdateUpp = 0;
        }
        else
        {
            short frame;
            if (n.UpdaterFlag == 1 || n.UpdaterFlag == 3)
            {
                if (n.UpdaterFlag == 3)
                {
                    frame = (short)HalfTowardZero(n.UpdaterPayload);
                }
                else
                {
                    frame = (short)n.UpdaterPayload;
                }
                if (frame < 3)
                {
                    n.SpritePtr = SpriteFrameTables.StreakFrames[n.SortKey * StreakFramesPerRow + frame];
                }
                else if (frame < 6)
                {
                    n.SpritePtr = SpriteFrameTables.StreakFrames[n.SortKey * StreakFramesPerRow + (5 - frame)];
                }
                else
                {
                    n.SpritePtr = 0;
                    n.UpdaterPayload = -1;
                }
            }
            else
            {
                if (n.UpdaterFlag == 2)
                {
                    frame = (short)HalfTowardZero(n.UpdaterPayload);
                }
                else
                {
                    frame = (short)n.UpdaterPayload;
                }
                if (frame < 8)
                {
                    n.SpritePtr = SpriteFrameTables.StreakFrames[n.SortKey * StreakFramesPerRow + frame];
                }
                else
                {
                    n.SpritePtr = 0;
                    n.UpdaterPayload = -1;
                }
            }

            var width = (short)MacRectWidth.Run(n.SpritePtr);
            n.PosX =
                (short)(int)(((float)WorldFlags.CameraCentreX +
                             ((float)n.SpawnPosX - ShipTable.PosX)) -
                            (float)HalfTowardZero(width));
            var height = (short)MacRectHeight.Run(n.SpritePtr);
            n.PosY =
                (short)(int)(((float)WorldFlags.CameraCentreY +
                             ((float)n.SpawnPosY - ShipTable.PosY)) -
                            (float)HalfTowardZero(height));

            if (WorldFlags.StreaksActiveFlag != 0)
            {
                n.UpdaterPayload++;
            }
            if (GamePrefs.GfxDetailFlag != 0)
            {
                junkcode.FUN_10060094();
            }
        }
    }
}
