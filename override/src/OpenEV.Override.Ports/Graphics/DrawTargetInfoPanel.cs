using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10056a2c (EV Override-11.c lines 35578-35885): the in-game TARGET
// info panel (bottom block of the right HUD column, rows 263..378). Caches the
// target's shield/class into the HUD-scheduler cells, redraws the panel into the
// backdrop GWorld (target name, target-ship PICT, shield/armor %, Fighter/Escort or
// captain-name tag), then composites it to the screen. Holding the debug key
// (keymap bit 0x33, gated by RenderGlobals.TargetDebugPanelFlag) draws the
// developer target-state readout instead.
public static class DrawTargetInfoPanel
{
    public static void Run()
    {
        // Managed {top, left, bottom, right} short[4] Rects.
        short[] panelRect = new short[4];      // the panel rect on the backdrop
        short[] backdropRect = new short[4];   // the panel-art src rect
        short[] targetPictRect = new short[4]; // the target-ship PICT rect

        int labelColor = UiColors.Friendly;
        int highlightColor = UiColors.Neutral;
        if (GameData.Player.TargetSlot == -1)
        {
            RenderGlobals.HudCachedTargetShield = -1;
            RenderGlobals.HudCachedTargetClass = -1;
        }
        else
        {
            short tgtIdx0 = GameData.Player.TargetSlot;
            // The shield cell (+0x68) is a float holding an int amount: read its VALUE
            // via (int)Shield, NOT the float bit pattern (see DrawPlayerShieldBar).
            RenderGlobals.HudCachedTargetShield = (short)(int)GameData.Ships[tgtIdx0].Shield;
            RenderGlobals.HudCachedTargetClass = GameData.Ships[tgtIdx0].ShipClass;
        }
        RenderGlobals.HudCachedJamFlag = RenderGlobals.RadarHudJamFlag;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(2020);
        MacToolbox.TextSize(14);
        MacToolbox.SetRect(panelRect, (short)(RenderGlobals.BackdropPort.RectRight + -139), (short)(RenderGlobals.BackdropPort.RectTop + 263), (short)(RenderGlobals.BackdropPort.RectRight + -5),
                         (short)(RenderGlobals.BackdropPort.RectTop + 378));
        MacToolbox.SetRect(backdropRect, 5, 263, 139, 378);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2, backdropRect, panelRect, 0, 0);
        // Normal panel unless both the live debug key (RadarHudJamFlag) and the dev
        // enable (TargetDebugPanelFlag) are held.
        if (RenderGlobals.RadarHudJamFlag == 0 || RenderGlobals.TargetDebugPanelFlag == 0)
        {
            if (GameData.Player.TargetSlot == -1)
            {
                MacToolbox.RGBForeColor((uint)labelColor);
                DrawCenteredString.Run("No Target", panelRect[1], panelRect[3], (short)(panelRect[0] + 47));
            }
            else
            {
                MacToolbox.RGBForeColor((uint)highlightColor);
                short tgtIdx = GameData.Player.TargetSlot;
                bool hasMissionShipName = GameData.Ships[tgtIdx].GrudgeMissionIndex != -1
                    && GameData.Missions[GameData.Ships[tgtIdx].GrudgeMissionIndex].Name.Length > 0;
                if (hasMissionShipName)
                {
                    DrawCenteredString.Run(GameData.Missions[GameData.Ships[tgtIdx].GrudgeMissionIndex].Name,
                                 panelRect[1], panelRect[3], (short)(panelRect[0] + 16));
                }
                else if (GameData.Ships[tgtIdx].PersIndex == -1)
                {
                    DrawCenteredString.Run(GameData.ShipClasses[GameData.Ships[tgtIdx].ShipClass].Name,
                                 panelRect[1], panelRect[3], (short)(panelRect[0] + 16));
                }
                else
                {
                    DrawCenteredString.Run(MacToolbox.PascalToString(GameData.Pers[GameData.Ships[tgtIdx].PersIndex].Name),
                                 panelRect[1], panelRect[3], (short)(panelRect[0] + 16));
                }
                // panel centre = (0.5*(left+right), 0.5*(top+bottom)).
                int scaledX = (int)(0.5 * (double)(panelRect[1] + panelRect[3]));
                int scaledY = (int)(0.5 * (double)(panelRect[0] + panelRect[2]));
                MacToolbox.SetRect(targetPictRect, (short)(scaledX + -64), (short)(scaledY + -32), (short)(scaledX + 64), (short)(scaledY + 32));
                MacToolbox.ForeColor(QuickDrawColor.Black);
                if (SpriteFrameTables.CommFacePicts[GameData.Ships[tgtIdx].ShipClass] != 0)
                {
                    MacToolbox.DrawPicture(SpriteFrameTables.CommFacePicts[GameData.Ships[tgtIdx].ShipClass],
                                       targetPictRect);
                }
                if (GameData.Ships[tgtIdx].PersIndex == ShipRecord.KamikazePersIndex)
                {
                    MacToolbox.RGBForeColor((uint)highlightColor);
                    DrawCenteredString.Run("Ambrosia", panelRect[1], panelRect[3], (short)(panelRect[2] + -6));
                }
                else
                {
                    MacToolbox.MoveTo(panelRect[1] + 5, panelRect[2] + -6);
                    if ((int)GameData.Ships[tgtIdx].Shield < 1)
                    {
                        MacToolbox.RGBForeColor((uint)highlightColor);
                        MacToolbox.MoveTo(panelRect[1] + 5, panelRect[2] + -6);
                        if ((GameData.ShipClasses[GameData.Ships[tgtIdx].ShipClass].Flags & ShipFlags.ShowArmorPercentOnTarget) == 0)
                        {
                            if (GameData.ShipClasses[GameData.Ships[tgtIdx].ShipClass].Shield < 1)
                            {
                                MacToolbox.DrawString("No Shields");
                            }
                            else
                            {
                                bool isDisabled = ShipDerivedStats.IsDisabled(ShipTable.Ships[tgtIdx]);
                                if (!isDisabled)
                                {
                                    MacToolbox.DrawString("Shields Down");
                                }
                                else
                                {
                                    MacToolbox.DrawString("Disabled");
                                }
                            }
                        }
                        else
                        {
                            MacToolbox.RGBForeColor((uint)labelColor);
                            bool isDisabled = ShipDerivedStats.IsDisabled(ShipTable.Ships[tgtIdx]);
                            if (!isDisabled)
                            {
                                MacToolbox.DrawString("Armor: ");
                                MacToolbox.RGBForeColor((uint)highlightColor);
                                short armorMax = (short)(ShipDerivedStats.EffectiveArmorMax(ShipTable.Ships[tgtIdx]));
                                double armorMaxD = armorMax;
                                if (armorMaxD <= 0.0)
                                {
                                    MacToolbox.DrawString("N/A");
                                }
                                else
                                {
                                    // (int)Shield = the shield cell's int VALUE (negative = armor
                                    // damage), NOT the float bit pattern.
                                    int scaledPct = (int)(100.0 *
                                                 ((armorMaxD + (double)(int)GameData.Ships[tgtIdx].Shield) / armorMaxD));
                                    MacToolbox.DrawString(scaledPct.ToString());
                                    MacToolbox.DrawString("%");
                                }
                            }
                            else
                            {
                                MacToolbox.DrawString("Disabled");
                            }
                        }
                    }
                    else
                    {
                        MacToolbox.RGBForeColor((uint)labelColor);
                        MacToolbox.DrawString("Shield: ");
                        MacToolbox.RGBForeColor((uint)highlightColor);
                        uint shieldMax = ShipDerivedStats.EffectiveShieldMax(ShipTable.Ships[tgtIdx]);
                        // 100 * (shield VALUE) / (shield max); (int)Shield is the cell's int
                        // amount, NOT the float bit pattern.
                        int scaledPct = (int)(100.0 *
                                   ((double)(int)GameData.Ships[tgtIdx].Shield /
                                   (double)(int)shieldMax));
                        MacToolbox.DrawString(scaledPct.ToString());
                        MacToolbox.DrawString("%");
                    }
                    if (GameData.Ships[tgtIdx].Govt < 0)
                    {
                        if (GameData.Ships[tgtIdx].OwnerSlot == 0)
                        {
                            MacToolbox.RGBForeColor((uint)labelColor);
                            MacToolbox.MoveTo(panelRect[1] + 87, panelRect[2] + -6);
                            if (GameData.Ships[tgtIdx].AiBehaviorType == ShipAiType.NavalFighter)
                            {
                                MacToolbox.DrawString("Fighter");
                            }
                            else
                            {
                                MacToolbox.DrawString("Escort");
                            }
                        }
                    }
                    else
                    {
                        MacToolbox.RGBForeColor((uint)labelColor);
                        MacToolbox.MoveTo(panelRect[1] + 87, panelRect[2] + -6);
                        // STR# 6000 captain-name table, 0x100-byte Pascal slots indexed by Govt.
                        MacToolbox.DrawString(ResourceGlobals.NamesStr6000[
                                    GameData.Ships[tgtIdx].Govt]);
                    }
                }
            }
        }
        else
        {
            // Developer target-state readout (debug key + dev enable). All numbers were
            // NumToString into a stack buffer — now C# ToString (same decimal digits).
            if (GameData.Player.TargetSlot == -1)
            {
                GameData.Ships[0].TargetSlot = 0;
            }
            short tgtIdx = GameData.Player.TargetSlot;
            MacToolbox.MoveTo(panelRect[3] + -15, panelRect[0] + 20);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Player.TargetSlot.ToString());
            MacToolbox.MoveTo(panelRect[1] + 5, panelRect[0] + 20);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Ships[tgtIdx].SlotIndex.ToString());
            MacToolbox.MoveTo(panelRect[1] + 5, panelRect[0] + 40);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("hg: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            DrawAiStateLabel.Run(GameData.Ships[tgtIdx].AiState);
            MacToolbox.MoveTo(panelRect[1] + 70, panelRect[0] + 40);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("lg: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            DrawAiManeuverStateLabel.Run(GameData.Ships[tgtIdx].AiManeuverState);
            MacToolbox.MoveTo(panelRect[1] + 5, panelRect[0] + 55);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("tnav: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Ships[tgtIdx].NavTargetSpob.ToString());
            MacToolbox.MoveTo(panelRect[1] + 70, panelRect[0] + 55);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("twep: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Ships[tgtIdx].TargetSlot.ToString());
            MacToolbox.MoveTo(panelRect[1] + 5, panelRect[0] + 70);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("cnt: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Ships[tgtIdx].AiActionTimer.ToString());
            MacToolbox.MoveTo(panelRect[1] + 70, panelRect[0] + 70);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("wp: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Ships[tgtIdx].SelectedWeaponSlot.ToString());
            MacToolbox.MoveTo(panelRect[1] + 5, panelRect[0] + 85);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("sh: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(((int)GameData.Ships[tgtIdx].Shield).ToString());
            MacToolbox.MoveTo(panelRect[1] + 60, panelRect[0] + 85);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("ai: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            // Preserve the original numeric debug readout (not the enum name) — DDC-01's
            // developer target-state dump prints raw field values.
            MacToolbox.DrawString(((short)GameData.Ships[tgtIdx].AiBehaviorType).ToString());
            MacToolbox.MoveTo(panelRect[1] + 45, panelRect[0] + 100);
            MacToolbox.RGBForeColor((uint)highlightColor);
            if (GameData.Ships[tgtIdx].DefendedSpobIndex == -1)
            {
                if (GameData.Ships[tgtIdx].GrudgeMissionIndex != -1)
                {
                    MacToolbox.DrawString("Sp");
                }
            }
            else
            {
                MacToolbox.DrawString("Df ");
            }
            MacToolbox.MoveTo(panelRect[1] + 90, panelRect[0] + 100);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("gov: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.DrawString(GameData.Ships[tgtIdx].Govt.ToString());
            MacToolbox.MoveTo(panelRect[1] + 5, panelRect[0] + 100);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("dc: ");
            MacToolbox.RGBForeColor((uint)highlightColor);
            int deathTimer = (int)GameData.Ships[tgtIdx].DeathTimer;
            MacToolbox.DrawString(deathTimer.ToString());
            MacToolbox.MoveTo(panelRect[1] + 45, panelRect[0] + 100);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("du: ");
            // (no highlight RGBForeColor before this number — original quirk)
            MacToolbox.DrawString(GameData.Ships[tgtIdx].DudeSpawnIndex.ToString());
            if (GameData.Player.TargetSlot == 0)
            {
                GameData.Ships[0].TargetSlot = -1;
            }
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, panelRect, panelRect, 0, 0);
    }
}
