using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_10020184 (EV Override-11.c lines 14288-14358) — the per-frame
// update UPP for the 26 background-nebula/scenery render nodes spawned by
// SpawnBackgroundNebulaSprites (UPP global 0x10081168 → InvokeNodeUpdateUpp).
// Picks the node's sprite by the NebulaTable row's kind, positions it
// camera-relative, and wraps the row's world X/Y around the play area with a
// sprite-extent margin; a node that wrapped this frame draws nothing (sprite
// cleared) so it never streaks across the screen.
public static class TickBackgroundNebulaSprite
{
    public static void Run(int nodePtr)
    {
        var n = SpriteNodes.At(nodePtr);
        if (n.ObjectPtr == 0)
        {
            n.UpdateUpp = 0;
            return;
        }

        var row = NebulaTable.At(n.ObjectPtr);
        if (row.Kind == 1)
        {
            n.SpritePtr = DockingDebrisFrameTables.DebrisPair[1];
        }
        else
        {
            n.SpritePtr = DockingDebrisFrameTables.DebrisPair[0];
        }
        short spriteW = (short)MacRectWidth.Run(n.SpritePtr);
        short spriteH = (short)MacRectHeight.Run(n.SpritePtr);
        short playW = (short)(GlobalState.PortRight - 144 - GlobalState.PortLeft);   // 144 = status panel
        short playH = (short)(GlobalState.PortBottom - GlobalState.PortTop);
        n.PosX = (short)(int)(row.X - ShipTable.PosX);
        n.PosY = (short)(int)(row.Y - ShipTable.PosY);

        bool wrappedLeft = (int)n.PosX < (int)GlobalState.PortLeft - (int)spriteW;
        if (wrappedLeft)
        {
            row.X += (float)(playW + spriteW * 2);
        }
        bool wrappedRight = (int)GlobalState.PortRight + (int)spriteW - 144 < (int)n.PosX;   // 144 = status panel
        if (wrappedRight)
        {
            row.X -= (float)(playW + spriteW * 2);
        }
        bool wrappedTop = (int)n.PosY < (int)GlobalState.PortTop - (int)spriteH;
        if (wrappedTop)
        {
            row.Y += (float)(playH + spriteH * 2);
        }
        bool wrappedBottom = (int)GlobalState.PortBottom + (int)spriteH < (int)n.PosY;
        if (wrappedBottom)
        {
            row.Y -= (float)(playH + spriteH * 2);
        }

        if (wrappedBottom || wrappedTop || wrappedRight || wrappedLeft)
        {
            n.SpritePtr = 0;
        }
        else
        {
            n.PosX = (short)(int)(row.X - ShipTable.PosX);
            n.PosY = (short)(int)(row.Y - ShipTable.PosY);
        }
        if (GamePrefs.GfxDetailFlag != 0)
        {
            junkcode.FUN_10060094();
        }
    }
}
