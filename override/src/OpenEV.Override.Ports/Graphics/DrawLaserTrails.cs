using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10022648 (EV Override-11.c lines 15081-15133).
public static class DrawLaserTrails
{
    public static void Run()
    {
        // ASM-verified (FUN_10022648): the compiled loop is entry-jump-to-test
        // shaped (li r31,0; b loc_22868), the same for-loop shape as the sibling
        // DrawHyperspaceLanes -- not the do-while+goto the decompile renders it as.
        for (short laneIndex = 0; laneIndex < BeamTable.Count; laneIndex = (short)(laneIndex + 1))
        {
            var beam = GameData.Beams[laneIndex];
            if (-2 < beam.Life)
            {
                if (beam.OwnerSlot != -1)
                {
                    if (0 < beam.Life && beam.StartX == beam.PrevStartX)
                    {
                        if (beam.StartY == beam.PrevStartY)
                        {
                            if (WorldState.GameFrameTickCounter % 3 != 0) continue;
                        }
                    }
                    if (beam.PrevStartX <= GlobalState.PortRight - 149)
                    {
                        if (beam.PrevEndX <= GlobalState.PortRight - 152)
                        {
                            SetGamePortAndDevice.Run();
                            MacToolbox.ForeColor(QuickDrawColor.Black);
                            MacToolbox.MoveTo(beam.PrevStartX, beam.PrevStartY);
                            MacToolbox.PenSize(GameData.Weapons[beam.WeaponType].ShotOffset + 2,
                                               GameData.Weapons[beam.WeaponType].ShotOffset + 2);
                            MacToolbox.LineTo(beam.PrevEndX, beam.PrevEndY);
                            MacToolbox.PenSize(1, 1);
                        }
                    }
                }
            }
        }
        SetGamePortAndDevice.Run();
        MacToolbox.PenSize(1, 1);
        MacToolbox.ForeColor(QuickDrawColor.Black);
    }
}
