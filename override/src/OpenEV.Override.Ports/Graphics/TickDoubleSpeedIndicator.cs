using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1002043c — per-frame updater for the persistent 2x-game-speed indicator node
// (the node+0x1a UPP; Combat.SpawnHudOverlayNodes spawns it). Absent from the
// decompile (mixed-mode jumptable, reached via UPP TVector off_824D0); ported from the
// disassembly at loc_2043C, so there is no EV Override-11.c line range.
//
// Per frame: when Caps-Lock 2x game speed is active (WorldState.DoubleSpeedActive),
// stamp the cicn-20000 frame into the node at the play-area top-left (0,0) so the shared
// layout pass blits it; otherwise clear its sprite (SpritePtr 0 = hidden).
public static class TickDoubleSpeedIndicator
{
    public static void Run(int node)
    {
        var n = SpriteNodes.At(node);
        if (WorldState.DoubleSpeedActive != 0)
        {
            n.SpritePtr = DockingDebrisFrameTables.Cicn20000Cell[0];
            n.PosY = 0;   // play-area top
            n.PosX = 0;   // play-area left
        }
        else
        {
            n.SpritePtr = 0;   // hidden
        }
    }
}
