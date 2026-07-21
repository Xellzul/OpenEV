using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_100542d8 (EV Override-11.c lines 34441-34512): reseed all 26
// NebulaTable rows — random world X/Y inside the play area, random kind (0/1),
// random angle (0..359) and parallax depth — then wrap each row once against
// the camera the same way TickBackgroundNebulaSprite does per frame.
public static class ReseedBackgroundNebulae
{
    public static void Run()
    {
        int playWidth = GlobalState.PortRight - GlobalState.PortLeft - 144;
        short topEdge = GlobalState.PortTop;
        short bottomEdge = GlobalState.PortBottom;

        foreach (var row in GameData.Nebulas)
        {
            short rand = (short)SeedEvoRng.Run((short)playWidth);
            row.X = rand;
            rand = (short)SeedEvoRng.Run((short)(bottomEdge - topEdge));
            row.Y = rand;
            row.Kind = (short)SeedEvoRng.Run(2);
            row.Angle = (short)SeedEvoRng.Run(360);
            rand = (short)SeedEvoRng.Run((short)(int)(NebulaTable.DepthScale * (short)playWidth));
            row.Depth = rand;

            // One wrap pass against the camera (same shape as the per-frame updater,
            // but with no sprite-extent margin).
            short screenX = (short)(int)(row.X - ShipTable.PosX);
            short screenY = (short)(int)(row.Y - ShipTable.PosY);
            if (screenX < 0)
                row.X += playWidth;
            if (playWidth < screenX)
                row.X -= playWidth;
            if (screenY < 0)
                row.Y += bottomEdge - topEdge;
            // BUG (OGB-21, kept): the Y-overflow check compares against the play WIDTH
            // (right-left-144), not the height (bottom-top) — rows with Y between the
            // height and the width never wrap down here (dec:34499).
            if (playWidth < screenY)
                row.Y -= bottomEdge - topEdge;
        }
    }
}
