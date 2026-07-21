using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10020728 (EV Override-11.c lines 14443-14530).
// `nodePtr` is a managed SpriteNode handle (one of the 4 docking-ring corner nodes
// SpawnHudOverlayNodes allocates); UpdaterPayload holds the corner index 0-3.
public static class TickDockingRing
{
    public static void Run(int nodePtr)
    {
        const double HalfScale = 0.5;   // _DAT_10081df0 (PEF data-seg double constant)

        var n = SpriteNodes.At(nodePtr);
        var player = ShipTable.Player;
        n.SpritePtr = 0;
        if (player.NavMode == 2 && player.NavTargetSpob != -1 &&
            player.CurrentSystem == GameData.Spobs[player.NavTargetSpob].System)
        {
            var spob = GameData.Spobs[player.NavTargetSpob];
            if (spob.TradingEnabled == 0)
            {
                n.SpritePtr = DockingDebrisFrameTables.DockingRingDim[n.UpdaterPayload];
            }
            else
            {
                n.SpritePtr = DockingDebrisFrameTables.DockingRingLit[n.UpdaterPayload];
            }
            short spriteWidth = (short)MacRectWidth.Run(n.SpritePtr);
            n.PosX =
                 (short)(int)-(HalfScale * (double)spriteWidth -
                              (double)((float)WorldFlags.CameraCentreX +
                                      ((float)spob.XPos - ShipTable.PosX)));
            short spriteHeight = (short)MacRectHeight.Run(n.SpritePtr);
            n.PosY =
                 (short)(int)-(HalfScale * (double)spriteHeight -
                              (double)((float)WorldFlags.CameraCentreY +
                                      ((float)spob.YPos - ShipTable.PosY)));

            ushort targetSpriteWidth = (ushort)MacRectWidth.Run(PlanetSpriteRecordTable.Store[spob.SpriteId]);
            ushort ringSpriteWidth = (ushort)MacRectWidth.Run(n.SpritePtr);
            // ASM computes this via srawi+addze — a truncating division by 2, not a plain shift; keep as /2.
            short xOffset = (short)((short)targetSpriteWidth / 2 - (short)ringSpriteWidth / 2 + 3);

            ushort targetSpriteHeight = (ushort)MacRectHeight.Run(PlanetSpriteRecordTable.Store[spob.SpriteId]);
            ushort ringSpriteHeight = (ushort)MacRectHeight.Run(n.SpritePtr);
            // ASM computes this via srawi+addze — a truncating division by 2, not a plain shift; keep as /2.
            short offset = (short)((short)targetSpriteHeight / 2 - (short)ringSpriteHeight / 2 + 3);

            if (offset < xOffset)
            {
                offset = xOffset;
            }
            if (offset < 16)
            {
                offset = 16;
            }
            if (n.UpdaterPayload == 0)
            {
                n.PosX = (short)(n.PosX - offset);
                n.PosY = (short)(n.PosY - offset);
            }
            if (n.UpdaterPayload == 1)
            {
                n.PosX = (short)(n.PosX + offset);
                n.PosY = (short)(n.PosY - offset);
            }
            if (n.UpdaterPayload == 2)
            {
                n.PosX = (short)(n.PosX + offset);
                n.PosY = (short)(n.PosY + offset);
            }
            if (n.UpdaterPayload == 3)
            {
                n.PosX = (short)(n.PosX - offset);
                n.PosY = (short)(n.PosY + offset);
            }
            if (GamePrefs.GfxDetailFlag != 0)
            {
                junkcode.FUN_10060094();
            }
        }
    }
}
