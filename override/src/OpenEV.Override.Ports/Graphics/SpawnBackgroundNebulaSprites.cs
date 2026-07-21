using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10054240 (EV Override-11.c lines 34416-34440): allocate the 26
// background-nebula/scenery render nodes and point each at its NebulaTable row.
// The per-frame updater (TickBackgroundNebulaSprite) wraps each row around the
// play area.
public static class SpawnBackgroundNebulaSprites
{
    public static void Run()
    {
        int nebulaUpdateUpp = Misc.SpriteNodeUppCells.NebulaUpdateUpp;
        for (short nebulaIndex = 0; nebulaIndex < NebulaTable.Count; nebulaIndex = (short)(nebulaIndex + 1))
        {
            int node = AllocateSpriteRecord.Run(0, 0, 0, 0);
            if (node != 0)
            {
                var n = SpriteNodes.At(node);
                n.UpdateUpp = nebulaUpdateUpp;
                n.SortKey = 0;
                n.State = 0;
                n.UpdaterFlag = 0;
                n.ObjectPtr = NebulaTable.Base + nebulaIndex * NebulaTable.Stride;
            }
        }
    }
}
