using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Systems;

// FUN_1006a284 — the "AnimUpdate" per-frame node updater (node+0x1a UPP) for the
// ambient drifting-asteroid sprites (the original misnamed them "dust";
// Systems.Model.AsteroidTable, spawned by Graphics.SpawnWorldSpriteNodes). Absent
// from the decompile (mixed-mode jumptable, reached via UPP TVector
// off_825A0 = SpriteNodeUppCells.AnimUpdateUpp); ported from the disassembly
// loc_6A284, so there is no EV Override-11.c line range.
//
// Per frame, for one asteroid node: cull if the slot died, else drift the particle
// by its velocity, advance the rotation frame, stamp the current sprite +
// camera-relative screen position into the node (the shared render pass blits
// node.SpritePtr at node.PosX/PosY), and wrap the particle to the opposite edge
// when its node leaves the play area.
public static class TickAnimSprite
{
    // The two variants drift as spïn 800 (24px sprite, frame every 4th game-frame)
    // or spïn 801 (32px, every 8th). The half-extents (0xC / 0x10) centre the
    // sprite on the particle and set the screen-wrap margins.
    private const int Spin800HalfExtent = 12;   // 0xC
    private const int Spin801HalfExtent = 16;   // 0x10
    private const short DeadTimer = -32000;     // -0x7d00 — an expired/destroyed particle's Timer sentinel

    public static void Run(int node)
    {
        var n = SpriteNodes.At(node);
        int idx = n.UpdaterPayload;
        if (idx < 0 || idx >= AsteroidTable.Count)
        {
            DetachNode(n);
            return;
        }

        var ast = GameData.Asteroids[idx];
        // Cull a dead/destroyed slot, a system with no asteroids, or a flagged-expired Timer.
        if (ast.Active == 0
            || ast.Timer <= DeadTimer
            || WorldState.NoAsteroidsFlag != 0)
        {
            ast.Active = 0;
            ast.Spawned = 0;
            DetachNode(n);
            return;
        }

        ast.PosX += ast.VelX;
        ast.PosY += ast.VelY;

        // Advance the rotation frame and stamp the node's current sprite.
        int frameTick = WorldState.GameFrameTickCounter;
        int halfExtent;
        if (ast.SpriteVariant == 0)
        {
            if (frameTick % 4 == 0)
                ast.AnimFrame = (short)(ast.AnimFrame + ast.Direction);
            if (ast.AnimFrame < 0) ast.AnimFrame = (short)(ast.AnimFrame + 20);   // wrap into [0,20)
            if (ast.AnimFrame >= 20) ast.AnimFrame = (short)(ast.AnimFrame - 20);
            n.SpritePtr = SpriteFrameTables.Spin800Frames[ast.AnimFrame];
            halfExtent = Spin800HalfExtent;
        }
        else
        {
            if (frameTick % 8 == 0)
                ast.AnimFrame = (short)(ast.AnimFrame + ast.Direction);
            if (ast.AnimFrame < 0) ast.AnimFrame = (short)(ast.AnimFrame + 30);   // wrap into [0,30)
            if (ast.AnimFrame >= 30) ast.AnimFrame = (short)(ast.AnimFrame - 30);
            n.SpritePtr = SpriteFrameTables.Spin801Frames[ast.AnimFrame];
            halfExtent = Spin801HalfExtent;
        }

        // Camera-relative screen position: top-left = centre + (world - player) - half,
        // truncated toward zero (PPC fctiwz) — keep the (short)(int)(double ...)
        // truncation chain, dropping it changes the rounding for out-of-range values.
        // POSSIBLE DEVIATION (pre-existing, unconfirmed, not fixed by this pass): the ASM
        // keeps this whole accumulation in SINGLE precision (fadds/fsubs — camX/halfExtent
        // go through the int->double magic-bias idiom only to convert them, then get
        // rounded back to float immediately), whereas this port accumulates in double via
        // the (double)camX cast. The two can differ by up to 1 ULP of float precision at a
        // screen-position boundary. Flagged for a dedicated faithfulness audit.
        short camX = WorldFlags.CameraCentreX;
        short camY = WorldFlags.CameraCentreY;
        float playerX = GameData.Ships[0].PosX, playerY = GameData.Ships[0].PosY;
        n.PosX = (short)(int)((double)camX + (ast.PosX - playerX) - halfExtent);
        n.PosY = (short)(int)((double)camY + (ast.PosY - playerY) - halfExtent);

        // Wrap the particle to the opposite edge once its node leaves the play area.
        // The margin depends on the graphics-detail pref (GamePrefs.GfxDetailFlag).
        if (GamePrefs.GfxDetailFlag != 0)
        {
            if (n.PosX > 2 * camX - 2 * halfExtent) ast.PosX = playerX - camX + halfExtent;
            if (n.PosX < 0) ast.PosX = playerX + camX - halfExtent;
            if (n.PosY > 2 * camY - 2 * halfExtent) ast.PosY = playerY - camY + halfExtent;
            if (n.PosY < 0) ast.PosY = playerY + camY - halfExtent;
        }
        else
        {
            if (n.PosX > 2 * camX) ast.PosX = playerX - camX - halfExtent;
            if (n.PosX < -2 * halfExtent) ast.PosX = playerX + camX + halfExtent;
            if (n.PosY > 2 * camY) ast.PosY = playerY - camY - halfExtent;
            if (n.PosY < -2 * halfExtent) ast.PosY = playerY + camY + halfExtent;
        }
    }

    // Detach a node from its asteroid: clear the sprite + index and zero the update
    // UPP so TickSpriteSystem's sweep frees it.
    private static void DetachNode(SpriteNode n)
    {
        n.SpritePtr = 0;
        n.UpdateUpp = 0;
        n.UpdaterPayload = -1;
    }
}
