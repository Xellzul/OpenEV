using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100228ac (EV Override-11.c lines 15134-15264).
public static class DrawHyperspaceLanes
{
    public static void Run()
    {
        // Draws the hyperspace nav lanes during in-game flight. The two data-seg
        // doubles are now C# literals: toc-0x6870 = 0.5 (pen-width scale),
        // toc-0x6888 = the i2d bias.
        for (short laneIndex = 0; laneIndex < BeamTable.Count; laneIndex = (short)(laneIndex + 1))
        {
            if (-1 < GameData.Beams[laneIndex].Life)
            {
                if (GameData.Beams[laneIndex].OwnerSlot != -1)
                {
                    if (GameData.Beams[laneIndex].StartX <= GlobalState.PortRight - 149)
                    {
                        if (GameData.Beams[laneIndex].EndX <= GlobalState.PortRight - 152)
                        {
                            SetGamePortAndDevice.Run();
                            if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex < -7)
                            {
                                // ASM-verified (FUN_100228ac): the compiled code really does
                                // re-test this same "< -7" condition again immediately inside
                                // its own true branch -- a dead-but-faithful double guard, not
                                // a transcription artifact. Keep it; don't collapse to a single check.
                                if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex < -7)
                                {
                                    if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -8)
                                    {
                                        MacToolbox.ForeColor(QuickDrawColor.Red);
                                    }
                                    if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -9)
                                    {
                                        MacToolbox.ForeColor(QuickDrawColor.Green);
                                    }
                                    if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -10)
                                    {
                                        MacToolbox.ForeColor(QuickDrawColor.Blue);
                                    }
                                    MacToolbox.MoveTo(GameData.Beams[laneIndex].StartX, GameData.Beams[laneIndex].StartY);
                                    MacToolbox.PenSize(GameData.Weapons[GameData.Beams[laneIndex].WeaponType].ShotOffset,
                                               GameData.Weapons[GameData.Beams[laneIndex].WeaponType].ShotOffset);
                                    MacToolbox.LineTo(GameData.Beams[laneIndex].EndX, GameData.Beams[laneIndex].EndY);
                                    if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -8)
                                    {
                                        MacToolbox.ForeColor(QuickDrawColor.Magenta);
                                    }
                                    if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -9)
                                    {
                                        MacToolbox.ForeColor(QuickDrawColor.Yellow);
                                    }
                                    if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -10)
                                    {
                                        MacToolbox.ForeColor(QuickDrawColor.Cyan);
                                    }
                                    MacToolbox.MoveTo(GameData.Beams[laneIndex].StartX, GameData.Beams[laneIndex].StartY);
                                    // Half beam width -- same signed int->double idiom as the
                                    // full-width PenSize above (== (double)ShotOffset), just
                                    // scaled by the data-seg 0.5 literal (toc-0x6870).
                                    MacToolbox.PenSize((int)(0.5 * GameData.Weapons[GameData.Beams[laneIndex].WeaponType].ShotOffset),
                                               (int)(0.5 * GameData.Weapons[GameData.Beams[laneIndex].WeaponType].ShotOffset));
                                    MacToolbox.LineTo(GameData.Beams[laneIndex].EndX, GameData.Beams[laneIndex].EndY);
                                }
                            }
                            else
                            {
                                if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -2)
                                {
                                    MacToolbox.ForeColor(QuickDrawColor.Red);
                                }
                                else if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -3)
                                {
                                    MacToolbox.ForeColor(QuickDrawColor.Green);
                                }
                                else if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -4)
                                {
                                    MacToolbox.ForeColor(QuickDrawColor.Blue);
                                }
                                else if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -5)
                                {
                                    MacToolbox.ForeColor(QuickDrawColor.Cyan);
                                }
                                else if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -6)
                                {
                                    MacToolbox.ForeColor(QuickDrawColor.Magenta);
                                }
                                else if (GameData.Weapons[GameData.Beams[laneIndex].WeaponType].SpriteIndex == -7)
                                {
                                    MacToolbox.ForeColor(QuickDrawColor.Yellow);
                                }
                                MacToolbox.MoveTo(GameData.Beams[laneIndex].StartX, GameData.Beams[laneIndex].StartY);
                                MacToolbox.PenSize(GameData.Weapons[GameData.Beams[laneIndex].WeaponType].ShotOffset,
                                             GameData.Weapons[GameData.Beams[laneIndex].WeaponType].ShotOffset);
                                MacToolbox.LineTo(GameData.Beams[laneIndex].EndX, GameData.Beams[laneIndex].EndY);
                            }
                            MacToolbox.PenSize(1, 1);
                            MacToolbox.ForeColor(QuickDrawColor.Black);
                        }
                    }
                }
            }
        }
        SetGamePortAndDevice.Run();
        MacToolbox.PenSize(1, 1);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        return;
    }
}
