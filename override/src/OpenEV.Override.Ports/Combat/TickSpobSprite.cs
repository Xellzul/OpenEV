using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10020548 (EV Override-11.c lines 14392-14442).
public static class TickSpobSprite
{
    public static void Run(int renderNode)
    {
        var n = SpriteNodes.At(renderNode);
        n.SpritePtr = 0;
        int spobPtr = n.ObjectPtr;
        if (spobPtr == 0) { n.UpdateUpp = 0; return; }

        var spob = SpobTable.FromPtr(spobPtr);
        var player = GameData.Player;
        if (spob.System != player.CurrentSystem || spob.Visible == 0)
        {
            n.SpritePtr = 0;
            n.UpdateUpp = 0;
            spob.Spawned = 0;
            return;
        }

        int rec = PlanetSpriteRecordTable.Store[spob.SpriteId];
        n.SpritePtr = rec;
        int spW = (short)MacRectWidth.Run(rec);
        int spH = (short)MacRectHeight.Run(rec);

        // Camera-relative screen position (sprite-centred). Spob X/Y are int16 world
        // coords converted directly to float — no coordinate divisor (that scale is
        // the galaxy-map zoom, not in-system bodies).
        int scrCX = WorldFlags.CameraCentreX;
        int scrCY = WorldFlags.CameraCentreY;
        float camX = player.PosX;
        float camY = player.PosY;
        short spobX = spob.XPos;
        short spobY = spob.YPos;
        // ASM computes this via srawi+addze — a truncating division by 2, not a plain shift; keep as /2.
        n.PosX = (short)(int)((scrCX + (spobX - camX)) - (spW / 2));
        n.PosY = (short)(int)((scrCY + (spobY - camY)) - (spH / 2));
    }
}
