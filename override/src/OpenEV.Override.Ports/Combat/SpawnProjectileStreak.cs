using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_100698e4 — EV Override-11.c lines 43738-43768. Spawns a projectile "streak" trail
// sprite at (spawnX, spawnY) if fewer than 64 streaks are active and streaks aren't
// disabled. `streakType` selects the trail's frame-stepping pattern (TickStreakSprite);
// `streakRow` selects which row of StreakFrames the trail draws from.
public static class SpawnProjectileStreak
{
    private const int MaxActiveStreaks = 64;

    public static void Run(float spawnX, float spawnY, short streakType, short streakRow)
    {
        if (EvoGlobals.ActiveStreakCount >= MaxActiveStreaks ||
            GamePrefs.ProjectileStreaksDisabled != 0)
        {
            return;
        }

        var streakPtr = AllocateSpriteRecord.Run(0, 0, 0, 0);
        if (streakPtr == 0)
        {
            return;
        }

        var node = SpriteNodes.At(streakPtr);
        node.UpdateUpp = SpriteNodeUppCells.StreakUpdateUpp;
        node.CollisionUpp = 0;
        node.TeardownUpp = 0;
        node.UpdaterFlag = 0;
        node.SortKey = 4;
        node.SpawnPosX = (short)(int)spawnX;
        node.SpawnPosY = (short)(int)spawnY;
        node.UpdaterFlag = streakType;
        node.UpdaterPayload = 0;
        node.SortKey = streakRow;
        EvoGlobals.ActiveStreakCount++;
    }
}
