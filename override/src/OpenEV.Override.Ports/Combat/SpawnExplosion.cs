namespace OpenEV.Override.Ports.Combat;

using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

// FUN_100697a0 — EV Override-11.c lines 43692-43737. Spawns an explosion sprite node at
// (posX, posY) if fewer than 16 explosions are active. `type` picks the render priority;
// `sizeClass` picks the base lifetime (7 / 14 / 28 frames), which `lifetimeOffset` adjusts.
public static class SpawnExplosion
{
    private const int MaxActiveExplosions = 16;

    public static void Run(float posX, float posY, short type, short sizeClass, short lifetimeOffset)
    {
        if (EvoGlobals.ActiveExplosionCount >= MaxActiveExplosions)
            return;

        int explosionPtr = AllocateSpriteRecord.Run(0, 0, 0, 0);
        if (explosionPtr == 0)
            return;

        var node = SpriteNodes.At(explosionPtr);
        node.UpdateUpp = SpriteNodeUppCells.ExplosionUpdateUpp;
        node.CollisionUpp = 0;
        node.TeardownUpp = 0;
        node.State = 0;
        node.UpdaterFlag = 0;
        node.SortKey = (short)(type == 0 ? 17 : 11);
        node.SpawnPosX = (short)(int)posX;
        node.SpawnPosY = (short)(int)posY;
        node.UpdaterFlag = sizeClass;

        if (sizeClass == 0) node.UpdaterPayload = 7;
        if (sizeClass == 1) node.UpdaterPayload = 14;
        if (sizeClass == 2) node.UpdaterPayload = 28;
        node.UpdaterPayload += lifetimeOffset;

        EvoGlobals.ActiveExplosionCount++;
    }
}
