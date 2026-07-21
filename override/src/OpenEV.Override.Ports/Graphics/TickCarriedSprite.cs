using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1001f9ac (EV Override-11.c lines 14069-14125): per-frame update of one
// carried sprite (e.g. a deployed mine/decoy). Ages its life counter, advances + wraps
// its spin frame, integrates position by velocity*time-scale, then computes its
// camera-relative screen position. spriteNode is a managed SpriteNode HANDLE; the
// carried record is a typed DebrisRecord — ObjectPtr holds the DebrisTable SLOT INDEX.
// NOT YET ROUTED: EscapePodUpdateUpp still dispatches to the default no-op; when wiring
// this updater, revisit the ObjectPtr==0 guard (originally a null record-POINTER check,
// but slot 0 is a valid index here).
public static class TickCarriedSprite
{
    public static void Run(int spriteNode)
    {
        var n = SpriteNodes.At(spriteNode);
        if (n.ObjectPtr == 0)
        {
            n.UpdateUpp = 0;
            return;
        }

        var pod = GameData.Debris[n.ObjectPtr];

        short life = (short)(pod.LifeRemaining - 1);
        pod.LifeRemaining = life;
        // ClearCarriedSpritesFlag = the world-reset purge gate (formerly DAT_1008f3bc).
        // When set, this tick FREES the carried sprite.
        if (life < 0 || WorldState.ClearCarriedSpritesFlag != 0)
        {
            pod.LifeRemaining = DebrisRecord.Killed;
            n.SpritePtr = 0;
            n.UpdateUpp = 0;
        }

        // Advance the spin frame by the spin rate, then wrap into [0, 35] (36 frames / rotation).
        pod.AnimFrame = (short)(pod.AnimFrame + pod.SpinDir);
        while (35 < pod.AnimFrame)
            pod.AnimFrame = (short)(pod.AnimFrame - 36);
        while (pod.AnimFrame < 0)
            pod.AnimFrame = (short)(pod.AnimFrame + 36);
        n.SpritePtr = SpriteFrameDimTable.Store[pod.AnimFrame];

        // Integrate position by velocity * world time-scale.
        double scale = WorldState.TimeScale;
        pod.PosX = pod.PosX + (float)((double)pod.VelX * scale);
        pod.PosY = pod.PosY + (float)((double)pod.VelY * scale);

        // Camera-relative screen position = camCentre + (carriedPos - playerPos) - halfExtent.
        // The decompile's float cast is just (float)(int)val.
        // The half-extent's ASM is srawi+addze — a truncating division by 2, not a plain shift; keep /2.
        int spriteW = (short)MacRectWidth.Run(n.SpritePtr);
        n.PosX = (short)(int)(((int)WorldFlags.CameraCentreX
                               + (pod.PosX - ShipTable.PosX)) - (spriteW / 2));
        int spriteH = (short)MacRectHeight.Run(n.SpritePtr);
        n.PosY = (short)(int)(((int)WorldFlags.CameraCentreY
                               + (pod.PosY - ShipTable.PosY)) - (spriteH / 2));

        if (GamePrefs.GfxDetailFlag != 0)
            junkcode.FUN_10060094();
    }
}
