using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10020ad4 (EV Override-11.c lines 14531-14630). nodePtr is one of
// the 4 target-bracket corner SpriteNodes SpawnHudOverlayNodes allocates;
// UpdaterPayload (+0x58) holds the corner index 0-3. Picks the bracket-row
// sprite for the player's current target and positions this corner relative
// to the target's on-screen centre and the bracket sprite's half-extent.
public static class TickEscortTractor
{
    public static void Run(int nodePtr)
    {
        var node = SpriteNodes.At(nodePtr);
        node.SpritePtr = 0;

        var player = GameData.Ships[0];
        short targetSlot = player.TargetSlot;
        if (player.HasTargetLock != 1 || targetSlot == -1 ||
            player.CurrentSystem != GameData.Ships[targetSlot].CurrentSystem ||
            GameData.Ships[targetSlot].IsActive == 0 || GameData.Ships[targetSlot].HasWorldSpriteNode == 0)
        {
            return;
        }
        var target = GameData.Ships[targetSlot];

        // TargetBrackets is a 4-row x 4-corner table; UpdaterPayload selects the
        // corner within whichever row is picked here: row 0 engageable (default),
        // row 1 non-engageable, row 2 defended-spob, row 3 disabled.
        if (ShipDerivedStats.IsDisabled(ShipTable.Ships[targetSlot]))
        {
            node.SpritePtr = SpriteFrameTables.TargetBrackets[node.UpdaterPayload + 12];
        }
        else if (target.OwnerSlot == 0 && target.DefendedSpobIndex == -1)
        {
            node.SpritePtr = SpriteFrameTables.TargetBrackets[node.UpdaterPayload + 8];
        }
        else if (!ShipAi.IsEngageableTarget(ShipTable.Ships[targetSlot]))
        {
            node.SpritePtr = SpriteFrameTables.TargetBrackets[node.UpdaterPayload + 4];
        }
        else
        {
            node.SpritePtr = SpriteFrameTables.TargetBrackets[node.UpdaterPayload];
        }

        // Camera-relative screen position (bracket-sprite-centred). The ASM computes
        // the half-extent via srawi+addze — a truncating division by 2, not a plain
        // shift; keep as /2 (same idiom as TickSpobSprite).
        short bracketWidth = (short)MacRectWidth.Run(node.SpritePtr);
        short bracketHeight = (short)MacRectHeight.Run(node.SpritePtr);
        node.PosX = (short)(int)((WorldFlags.CameraCentreX + (target.PosX - player.PosX)) - bracketWidth / 2);
        node.PosY = (short)(int)((WorldFlags.CameraCentreY + (target.PosY - player.PosY)) - bracketHeight / 2);

        // Corner offset = half-extent of the target's weapon-graphics sprite at its
        // current ship-class/heading frame (max of width-half, height-half).
        int weaponFrameIndex = target.ShipClass * 36 + target.Heading / 10;
        short weaponWidth = (short)MacRectWidth.Run(WeaponGraphicsTable.Store[weaponFrameIndex]);
        short weaponHeight = (short)MacRectHeight.Run(WeaponGraphicsTable.Store[weaponFrameIndex]);
        int halfWidth = weaponWidth / 2;
        int halfHeight = weaponHeight / 2;
        short cornerOffset = (short)(halfHeight < halfWidth ? halfWidth : halfHeight);

        switch (node.UpdaterPayload)
        {
            case 0: node.PosX -= cornerOffset; node.PosY -= cornerOffset; break;
            case 1: node.PosX += cornerOffset; node.PosY -= cornerOffset; break;
            case 2: node.PosX += cornerOffset; node.PosY += cornerOffset; break;
            case 3: node.PosX -= cornerOffset; node.PosY += cornerOffset; break;
        }

        if (GamePrefs.GfxDetailFlag != 0)
        {
            junkcode.FUN_10060094();
        }
    }
}
