namespace OpenEV.Override.Ports.Combat;

using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.GalaxyMap;

// FUN_10052b38 — EV Override-11.c lines 33947-34008. Creates the persistent HUD / target-reticle
// overlay render nodes — two single overlay nodes plus two 4-corner node sets (the hostility-colored
// target brackets driven by TickEscortTractor, and the docking-ring set) — then sets up the HUD
// play-area clip rect.
public static class SpawnHudOverlayNodes
{
    private const short CornerCount = 4;

    public static void Run()
    {
        int nodePtr = AllocateSpriteRecord.Run(0, 0, 0, 0);
        EscortSpawnRecord.HudOverlayNode = nodePtr;
        if (nodePtr != 0)
        {
            var node = SpriteNodes.At(nodePtr);
            node.UpdateUpp = SpriteNodeUppCells.HudOverlayUpdateUpp;
            node.SortKey = 99;
            node.State = 0;
            node.UpdaterFlag = 0;
        }

        nodePtr = AllocateSpriteRecord.Run(0, 0, 0, 0);
        EscortSpawnRecord.Handle = nodePtr; // HUD blink-orb sprite node (was 0x1008a744); "Handle" is a
                                            // vague name inherited from CombatGraphicsTables.cs
        if (nodePtr != 0)
        {
            var node = SpriteNodes.At(nodePtr);
            node.UpdateUpp = SpriteNodeUppCells.HudBlinkOrbUpdateUpp;
            node.SortKey = 99;
            node.State = 0;
            node.UpdaterFlag = 0;
        }

        int reticleDrawUpp = SpriteNodeUppCells.ReticleDrawUpp;
        int reticleUpdateUpp = SpriteNodeUppCells.ReticleUpdateUpp;
        int dockingDrawUpp = SpriteNodeUppCells.DockingRingDrawUpp;
        int dockingUpdateUpp = SpriteNodeUppCells.DockingRingUpdateUpp;
        for (short corner = 0; corner < CornerCount; corner++)
        {
            nodePtr = AllocateSpriteRecord.Run(0, 0, 0, reticleDrawUpp);
            EscortSpawnRecord.ReticleNode = nodePtr;
            if (nodePtr != 0)
            {
                var node = SpriteNodes.At(nodePtr);
                node.UpdaterFlag = 0;
                node.UpdaterPayload = corner;
                node.UpdateUpp = reticleUpdateUpp;
                node.SortKey = 7;
                node.State = 0;
            }
            nodePtr = AllocateSpriteRecord.Run(0, 0, 0, dockingDrawUpp);
            EscortSpawnRecord.DockingRingNode = nodePtr;
            if (nodePtr != 0)
            {
                var node = SpriteNodes.At(nodePtr);
                node.UpdaterFlag = 0;
                node.UpdaterPayload = corner;
                node.UpdateUpp = dockingUpdateUpp;
                node.SortKey = 3;
                node.State = 0;
            }
        }

        short[] clipRect = GlobalState.HudPlayAreaClipRect;
        MacToolbox.SetRect(clipRect, (short)(GlobalState.PortLeft + 25), (short)(GlobalState.PortBottom - 5),
                           (short)(GlobalState.PortRight - 194), (short)(GlobalState.PortBottom - 5));
        int rectW = clipRect[3] - clipRect[1];
        // No zero-guard on rectW, matching the original; do not "fix" by adding one (bug-for-bug parity).
        clipRect[0] = (short)(clipRect[0] - 29470 / rectW);
        GalaxyMapState.UpdateRgn = MacToolbox.NewRgn();
    }
}
