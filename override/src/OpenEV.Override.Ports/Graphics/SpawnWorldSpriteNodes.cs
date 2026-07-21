using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10061d74 (EV Override-11.c lines 41062-41158). The per-frame
// SPRITE-NODE SPAWNER for ships / spobs / asteroids: allocates a render-list node
// for every live world object that doesn't have one yet and wires the per-type
// update/draw UPP tokens (Misc.SpriteNodeUppCells) into it.
public static class SpawnWorldSpriteNodes
{
    public static void Run()
    {
        int shipUpdateUpp = Misc.SpriteNodeUppCells.ShipUpdateUpp;
        int shipDrawUpp = Misc.SpriteNodeUppCells.ShipDrawUpp;
        int spobUpdateUpp = Misc.SpriteNodeUppCells.SpobUpdateUpp;
        int animUpdateUpp = Misc.SpriteNodeUppCells.AnimUpdateUpp;
        int animDrawUpp = Misc.SpriteNodeUppCells.AnimDrawUpp;
        for (short i = 0; i < ShipTable.Count; i = (short)(i + 1))
        {
            if (Core.Model.GameData.Ships[i].IsActive == 0)
            {
                Core.Model.GameData.Ships[i].HasWorldSpriteNode = 0;
            }
            else if ((Core.Model.GameData.Ships[0].CurrentSystem == Core.Model.GameData.Ships[i].CurrentSystem)
                    && (Core.Model.GameData.Ships[i].HasWorldSpriteNode == 0))
            {
                int node = AllocateSpriteRecord.Run(0, 0, 0, 0);
                if (node != 0)
                {
                    var n = SpriteNodes.At(node);
                    n.UpdateUpp = shipUpdateUpp;
                    n.CollisionUpp = shipDrawUpp;
                    n.TeardownUpp = 0;
                    n.State = 1;
                    n.UpdaterFlag = 1;
                    n.ObjectPtr = ShipTable.Ships[i];
                    Core.Model.GameData.Ships[i].HasWorldSpriteNode = 1;
                    if (i == 0)
                    {
                        n.SortKey = 15;
                    }
                    else
                    {
                        n.SortKey = 10;
                        if ((Core.Model.GameData.Ships[i].Govt != -1)
                            && ((Core.Model.GameData.Governments[Core.Model.GameData.Ships[i].Govt].Flags & GovtFlags.WarshipsPlunder) != 0))
                        {
                            n.SortKey = 11;
                        }
                    }
                }
            }
        }
        for (short i = 0; i < SystRecord.StellarLinkCount; i = (short)(i + 1))
        {
            short spob;
            int node;
            if (SystTable.SpobLink(Core.Model.GameData.Ships[0].CurrentSystem, i) != -1
                && Core.Model.GameData.Spobs[spob = SystTable.SpobLink(Core.Model.GameData.Ships[0].CurrentSystem, i)].Visible != 0
                && Core.Model.GameData.Ships[0].CurrentSystem == Core.Model.GameData.Spobs[spob].System
                && Core.Model.GameData.Spobs[spob].Spawned == 0
                && (node = AllocateSpriteRecord.Run(0, 0, 0, 0)) != 0)
            {
                var n = SpriteNodes.At(node);
                n.UpdateUpp = spobUpdateUpp;
                n.CollisionUpp = 0;
                n.TeardownUpp = 0;
                n.SortKey = 2;
                n.State = 0;
                n.UpdaterFlag = 0;
                n.ObjectPtr = new SpobRec(SpobTable.Base + spob * SpobTable.Stride);
                Core.Model.GameData.Spobs[spob].Spawned = 1;
            }
        }
        for (short i = 0; i < AsteroidTable.Count; i = (short)(i + 1))
        {
            int node;
            if (Core.Model.GameData.Asteroids[i].Active == 0)
            {
                Core.Model.GameData.Asteroids[i].Spawned = 0;
            }
            else if ((Core.Model.GameData.Asteroids[i].Spawned == 0)
                    && ((node = AllocateSpriteRecord.Run(0, 0, 0, 0)) != 0))
            {
                var n = SpriteNodes.At(node);
                n.UpdateUpp = animUpdateUpp;
                n.CollisionUpp = animDrawUpp;
                n.TeardownUpp = 0;
                n.State = 50;
                n.UpdaterFlag = 50;
                n.SortKey = 4;
                n.UpdaterPayload = i;
                n.ObjectPtr = 0;
                n.ExtentTop = 0;
                n.ExtentLeft = 0;
                if (Core.Model.GameData.Asteroids[i].SpriteVariant == 0)
                {
                    short spriteW = (short)MacRectWidth.Run(SpriteFrameTables.Spin800Frames[0]);
                    n.ExtentBottom = spriteW;
                    n.ExtentRight = spriteW;
                }
                else
                {
                    short spriteW = (short)MacRectWidth.Run(SpriteFrameTables.Spin801Frames[0]);
                    n.ExtentBottom = spriteW;
                    n.ExtentRight = spriteW;
                }
                Core.Model.GameData.Asteroids[i].Spawned = 1;
            }
        }
    }
}
