using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1001fbf8 (EV Override-11.c lines 14126-14199).
public static class TickExplosionSprite
{
    public static void Run(int spritePtr)
    {
        var node = SpriteNodes.At(spritePtr);
        node.UpdaterPayload--;
        if (node.UpdaterPayload < 0 || WorldState.ClearExplosionsFlag != 0)
        {
            EvoGlobals.ActiveExplosionCount--;
            node.SpritePtr = 0;
            node.UpdateUpp = 0;
        }
        else
        {
            // Below SpawnExplosion's per-size-class base lifetime (7/14/28), pick an
            // animation frame; above it (a delayed start via SpawnExplosion's
            // lifetimeOffset), stay invisible (SpritePtr left 0).
            short baseLifetime = 7;
            if (node.UpdaterFlag == 1)
            {
                baseLifetime = 14;
            }
            if (node.UpdaterFlag == 2)
            {
                baseLifetime = 28;
            }

            node.SpritePtr = 0;
            if (node.UpdaterPayload <= baseLifetime)
            {
                if (node.UpdaterFlag == 0)
                {
                    node.SpritePtr = ExplosionGraphicsTable.Store[node.UpdaterPayload];
                }
                if (node.UpdaterFlag == 1)
                {
                    node.SpritePtr = ExplosionGraphicsTable.Store[10 + node.UpdaterPayload / 2];
                }
                if (node.UpdaterFlag == 2)
                {
                    node.SpritePtr = ExplosionGraphicsTable.Store[20 + node.UpdaterPayload / 4];
                }
            }

            short width = (short)MacRectWidth.Run(node.SpritePtr);
            node.PosX = (short)(int)(((float)WorldFlags.CameraCentreX +
                                      ((float)node.SpawnPosX - ShipTable.PosX)) -
                                     (float)(width / 2));

            short height = (short)MacRectHeight.Run(node.SpritePtr);
            node.PosY = (short)(int)(((float)WorldFlags.CameraCentreY +
                                      ((float)node.SpawnPosY - ShipTable.PosY)) -
                                     (float)(height / 2));

            if (GamePrefs.GfxDetailFlag != 0)
            {
                junkcode.FUN_10060094();
            }
        }
    }
}
