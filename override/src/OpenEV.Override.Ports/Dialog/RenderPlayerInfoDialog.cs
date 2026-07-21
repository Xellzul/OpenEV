using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Combat;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1003f254 (EV Override-11.c lines 26021-26635) — draws the player-info
// dialog content for the current PAGE (WorldState.PlayerInfoPage:
// 1 = pilot/ship stats, 2 = cargo, 3 = extras) into the backdrop GWorld, then
// blits it to the dialog window. The CopyBits pixmap keys keep the numeric
// `+ 2` form; the STR# name-table reads go through the managed
// ResourceGlobals.NamesStr* string[] tables (truncated as the Mac strncpy did).
public static class RenderPlayerInfoDialog
{
    public static void Run()
    {
        int accentColor;
        byte flag;
        short i;
        uint bountySum;
        short j;
        short k;
        int intVal;
        var itemRect = new short[4];   // item-6 Rect {top,left,bottom,right}
        var insetRect = new short[4];   // TETextBox/InvertRect inset copy
        var itemHandle = new int[1];     // GetDialogItem handle out (never read back)
        var itemType = new short[1];   // GetDialogItem itemType out (never read back)

        accentColor = UiColors.Unexplored;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow));
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.GetDialogItem(PlayerInfoGlobals.DialogWindow, 6, itemType, itemHandle, itemRect);
        flag = (byte)(MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(PlayerInfoGlobals.DialogWindow)) ? 1 : 0);
        if (flag != 0)
        {
            // ── Page 1: pilot + ship stats ──
            if (WorldState.PlayerInfoPage == 1)
            {
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 12));
                MacToolbox.DrawString("Pilot Name:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 12));
                MacToolbox.DrawString(PilotIdentity.Name);
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 28));
                MacToolbox.DrawString("Current Date:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 28));
                MacToolbox.DrawString(FormatDateLongFull.Format(GameDate.Current.Year, GameDate.Current.Month,
                                                                 GameDate.Current.Day));
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 44));
                MacToolbox.DrawString("System:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 44));
                MacToolbox.DrawString(SystTable.Store[GameData.Ships[0].CurrentSystem].Name);
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 60));
                MacToolbox.DrawString("Legal Status:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 60));
                flag = (byte)(HasVisibleStellars.Run(GameData.Ships[0].CurrentSystem) ? 1 : 0);
                if (flag == 0)
                {
                    MacToolbox.DrawString("N/A");
                }
                else
                {
                    ResolveSystLegalStatusCategory.Run(GameData.Ships[0].CurrentSystem);
                }
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 76));
                MacToolbox.DrawString("Combat Rating:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 76));
                DrawCombatRatingName.Run();
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 205), (short)(itemRect[0] + 12));
                MacToolbox.DrawString("Ship Name:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 280), (short)(itemRect[0] + 12));
                MacToolbox.DrawString(PilotIdentity.ShipName);
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 205), (short)(itemRect[0] + 28));
                MacToolbox.DrawString("Ship Class:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 280), (short)(itemRect[0] + 28));
                MacToolbox.DrawString(GameData.ShipClasses[GameData.Ships[0].ShipClass].Name);
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 205), (short)(itemRect[0] + 44));
                MacToolbox.DrawString("Turn Rate:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 280), (short)(itemRect[0] + 44));
                i = (short)(ShipDerivedStats.EffectiveManeuver(ShipTable.Player));
                MacToolbox.DrawString((i * 30).ToString());
                MacToolbox.DrawString("°/sec");
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 205), (short)(itemRect[0] + 60));
                MacToolbox.DrawString("Accel Rate:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 280), (short)(itemRect[0] + 60));
                intVal = (int)(2500.0 * ShipDerivedStats.EffectiveAccel(ShipTable.Player)); // 0x10081ff8
                MacToolbox.DrawString(intVal.ToString());
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 205), (short)(itemRect[0] + 76));
                MacToolbox.DrawString("Max Speed:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 280), (short)(itemRect[0] + 76));
                if (WorldState.StrictPlay == 0)
                {
                    // strict-play display: 2/3 of the true speed (doubles 0x10081fe8 × float 0x10081ff0).
                    intVal = (int)((2.0 / 3.0) *
                                  (double)(float)((double)100f * ShipDerivedStats.EffectiveSpeed(ShipTable.Player)));
                }
                else
                {
                    intVal = (int)((double)100f * ShipDerivedStats.EffectiveSpeed(ShipTable.Player));
                }
                MacToolbox.DrawString(intVal.ToString());
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 205), (short)(itemRect[0] + 92));
                MacToolbox.DrawString("Credits:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 280), (short)(itemRect[0] + 92));
                FormatCredits.Run(GameData.Ships[0].Credits);
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 92));
                if ((int)GameData.Ships[0].Shield < 0)
                {
                    MacToolbox.DrawString("Armor Status:");
                }
                else
                {
                    MacToolbox.DrawString("Shield Status:");
                }
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 92));
                if ((int)GameData.Ships[0].Shield < 0)
                {
                    flag = (byte)((ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[0]) ? 1 : 0));
                    if (flag == 0)
                    {
                        // Shield is the int-valued float here (not a raw float bit-pattern) —
                        // use (int)Shield, not SingleToInt32Bits.
                        i = (short)(ShipDerivedStats.EffectiveArmorMax(ShipTable.Player));
                        intVal = (int)(100.0 *   // 0x10082038 (also the shield-% and fuel-% multiplier below)
                                      (double)((float)((int)GameData.Ships[0].Shield + i) /
                                              (float)(double)i));
                        MacToolbox.DrawString(intVal.ToString());
                        MacToolbox.DrawString("%");
                    }
                    else
                    {
                        MacToolbox.DrawString("Failed");
                    }
                }
                else
                {
                    // Same (int)Shield note as the armor branch above.
                    bountySum = ShipDerivedStats.EffectiveShieldMax(ShipTable.Player);
                    intVal = (int)(100.0 *
                                  (double)((float)(int)GameData.Ships[0].Shield /
                                          (float)(double)(int)bountySum));
                    MacToolbox.DrawString(intVal.ToString());
                    MacToolbox.DrawString("%");
                }
                MacToolbox.RGBForeColor((uint)accentColor);
                MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 108));
                MacToolbox.DrawString("Fuel Status:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo((short)(itemRect[1] + 80), (short)(itemRect[0] + 108));
                i = (short)(ShipDerivedStats.EffectiveFuelMax(ShipTable.Player));
                intVal = (int)(100.0 *
                              (double)(GameData.Ships[0].Fuel / (float)(double)i));
                MacToolbox.DrawString(intVal.ToString());
                MacToolbox.DrawString("%");
                intVal = (int)((double)GameData.Ships[0].Fuel / 100.0);   // 100 fuel = 1 jump
                i = (short)intVal;
                if (i < 1)
                {
                    if (0f < GameData.Ships[0].Fuel)   // 0x10082000
                    {
                        MacToolbox.DrawString("  (maneuvering fuel only)");
                    }
                }
                else
                {
                    MacToolbox.DrawString("  (");
                    MacToolbox.DrawString(((int)i).ToString());
                    MacToolbox.DrawString(" jump");
                    if (1 < i)
                    {
                        MacToolbox.DrawString("s");
                    }
                    if (0 < (int)(short)(int)GameData.Ships[0].Fuel % 100)
                    {
                        MacToolbox.DrawString(" plus maneuvering fuel");
                    }
                    MacToolbox.DrawString(")");
                }
                // Escort payments: 1% of each paid escort's class cost per day.
                bountySum = 0;
                intVal = 0;
                for (i = 1; i < ShipTable.Count; i = (short)(i + 1))
                {
                    if ((GameData.Ships[i].IsActive != 0) &&
                       (GameData.Ships[i].OwnerSlot == 0))
                    {
                        bool isFighter = false;
                        if ((GameData.Ships[i].GrudgeMissionIndex != -1) &&
                           (GameData.MissionStates[GameData.Ships[i].GrudgeMissionIndex].IsActive != 0))
                        {
                            for (j = GameData.Missions[GameData.Ships[i].GrudgeMissionIndex].ShipBehavior;
                                8 < j; j = (short)(j + -10))
                            {
                            }
                            if (j == 1)
                            {
                                isFighter = true;
                            }
                        }
                        if ((!isFighter) && (GameData.Ships[i].IsCarriedFighter != 0))
                        {
                            // 0.01 = the double at 0x10081fe0.
                            bountySum = (uint)(0.01 * (double)GameData.ShipClasses[GameData.Ships[i].ShipClass].Cost +
                                               (double)(int)bountySum);
                        }
                    }
                }
                if (0 < (int)bountySum)
                {
                    intVal = 16;
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 124));
                    MacToolbox.DrawString("Escort Payments:  ");
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    FormatCredits.Run((int)(bountySum));
                    MacToolbox.DrawString(" credits");
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.DrawString(" per day");
                }
                // Tribute: 1000 credits/day per tech level of each dominated trading spob.
                int cargoValue = 0;
                for (i = 0; i < SpobTable.Count; i = (short)(i + 1))
                {
                    if ((GameData.Spobs[i].Visible != 0) &&
                       (GameData.Spobs[i].TradingEnabled != 0))
                    {
                        cargoValue = cargoValue + GameData.Spobs[i].TechLevel * 1000;
                    }
                }
                if (0 < cargoValue)
                {
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + intVal + 124));
                    MacToolbox.DrawString("Tribute Received:  ");
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    FormatCredits.Run(cargoValue);
                    MacToolbox.DrawString(" credits");
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.DrawString(" per day");
                }
            }

            // ── Page 2: cargo aboard the ship / fleet ──
            if (WorldState.PlayerInfoPage == 2)
            {
                short[] typeCounts = new short[64];   // local_616 (local_658's per-type seen flags collapse into the -1 sentinel)
                // escortValue/escortCount accumulate but are never read again — a decompile
                // dead store (same class as the local_658 zero-fill below); weaponCount alone is read.
                short escortValue = 0;
                short weaponValue = 0;
                short escortCount = 0;
                short weaponCount = 0;
                for (i = 0; i < typeCounts.Length; i = (short)(i + 1))
                {
                    // (local_658[i] = 0 — a byte array the decompile zeroes here but never
                    // reads again; dead store dropped.)
                    typeCounts[i] = -1;
                }
                for (i = 0; i < ShipRecord.CargoHoldCount; i = (short)(i + 1))
                {
                    if (0 < GameData.Ships[0].CargoHold[i])
                    {
                        if (typeCounts[i] < 0)
                        {
                            typeCounts[i] = 0;
                        }
                        typeCounts[i] = (short)(typeCounts[i] + GameData.Ships[0].CargoHold[i]);
                    }
                }
                for (i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
                {
                    if (((GameData.MissionStates[i].IsActive != 0) &&
                        (GameData.Missions[i].CargoPickedUp != 0)) &&
                       (GameData.Missions[i].CargoStringIndex != -1))
                    {
                        escortCount = (short)(escortCount + 1);
                        escortValue = (short)(escortValue + GameData.Missions[i].CargoMass);
                        if (typeCounts[GameData.Missions[i].CargoStringIndex] < 0)
                        {
                            typeCounts[GameData.Missions[i].CargoStringIndex] = 0;
                        }
                        j = GameData.Missions[i].CargoStringIndex;
                        typeCounts[j] = (short)(typeCounts[j] + GameData.Missions[i].CargoMass);
                    }
                }
                i = 0;
                for (j = 0; j < typeCounts.Length; j = (short)(j + 1))
                {
                    if (-1 < typeCounts[j])
                    {
                        i = (short)(i + 1);
                    }
                }
                j = 0;
                while (true)
                {
                    if (j >= JunkTable.Count) break;
                    if (0 < GameData.Junk[j].PlayerQty)
                    {
                        weaponValue = (short)(weaponValue + GameData.Junk[j].PlayerQty);
                        weaponCount = (short)(weaponCount + 1);
                        i = (short)(i + 1);
                    }
                    j = (short)(j + 1);
                }
                if (i < 1)
                {
                    MacToolbox.RGBForeColor((uint)accentColor);
                    i = (short)(ShipDerivedStats.EffectiveCargoMax());
                    j = (short)(TotalMassWithEscorts.Run());
                    if (i < j)
                    {
                        DrawCenteredString.Run("You don’t have any cargo aboard your fleet’s ships",
                                     (short)((int)itemRect[1]), (short)((int)itemRect[3]),
                                     (short)(itemRect[0] + 55));
                    }
                    else
                    {
                        DrawCenteredString.Run("You don’t have any cargo aboard your ship",
                                     (short)((int)itemRect[1]), (short)((int)itemRect[3]),
                                     (short)(itemRect[0] + 55));
                    }
                }
                else
                {
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 12));
                    j = (short)(ShipDerivedStats.EffectiveCargoMax());
                    k = (short)(TotalMassWithEscorts.Run());
                    if (j < k)
                    {
                        MacToolbox.DrawString("Current cargo aboard your fleet’s ships:");
                    }
                    else
                    {
                        MacToolbox.DrawString("Current cargo aboard your ship:");
                    }
                    string text = "";   // (the old strncpy from the NUL at 0x10081fa7 just cleared the buffer)
                    j = 0;
                    for (k = 0; k < typeCounts.Length; k = (short)(k + 1))
                    {
                        if (typeCounts[k] != -1)
                        {
                            if (0 < typeCounts[k])
                            {
                                if (typeCounts[k] < 6)
                                {
                                    if (typeCounts[k] == 1)
                                    {
                                        text += "one";
                                    }
                                    else if (typeCounts[k] == 2)
                                    {
                                        text += "two";
                                    }
                                    else if (typeCounts[k] == 3)
                                    {
                                        text += "three";
                                    }
                                    else if (typeCounts[k] == 4)
                                    {
                                        text += "four";
                                    }
                                    else if (typeCounts[k] == 5)
                                    {
                                        text += "five";
                                    }
                                }
                                else
                                {
                                    text += ((int)typeCounts[k]).ToString();
                                }
                                if (typeCounts[k] == 1)
                                {
                                    text += " ton";
                                }
                                else
                                {
                                    text += " tons";
                                }
                                text += " of ";
                            }
                            // STR# 0xfa1 commodity name, max 19 chars (FUN_10076178 strncpy semantics).
                            text += TextScratch.Trunc(ResourceGlobals.NamesStr0fa1[k], 19);
                            if ((int)j < i + -2)
                            {
                                text += ", ";
                            }
                            if ((int)j == i + -2)
                            {
                                if (2 < i)
                                {
                                    text += ",";
                                }
                                text += " and ";
                            }
                            j = (short)(j + 1);
                        }
                    }
                    if (0 < weaponCount)
                    {
                        k = 0;
                        while (true)
                        {
                            if (k >= JunkTable.Count) break;
                            if (0 < GameData.Junk[k].PlayerQty)
                            {
                                if (GameData.Junk[k].PlayerQty < 6)
                                {
                                    if (GameData.Junk[k].PlayerQty == 1)
                                    {
                                        text += "one";
                                    }
                                    else if (GameData.Junk[k].PlayerQty == 2)
                                    {
                                        text += "two";
                                    }
                                    else if (GameData.Junk[k].PlayerQty == 3)
                                    {
                                        text += "three";
                                    }
                                    else if (GameData.Junk[k].PlayerQty == 4)
                                    {
                                        text += "four";
                                    }
                                    else if (GameData.Junk[k].PlayerQty == 5)
                                    {
                                        text += "five";
                                    }
                                }
                                else
                                {
                                    text += ((int)GameData.Junk[k].PlayerQty).ToString();
                                }
                                if (GameData.Junk[k].PlayerQty == 1)
                                {
                                    text += " ton";
                                }
                                else
                                {
                                    text += " tons";
                                }
                                text += " of ";
                                // The junk name (STR# 0xfa6) truncates to 19 chars here, same as the
                                // commodity name above — NOT 29 like the outfit-name STR#s on page 3.
                                text += TextScratch.Trunc(ResourceGlobals.NamesStr0fa6[k], 19);
                                if ((int)j < i + -2)
                                {
                                    text += ", ";
                                }
                                if ((int)j == i + -2)
                                {
                                    if (2 < i)
                                    {
                                        text += ",";
                                    }
                                    text += " and ";
                                }
                                j = (short)(j + 1);
                            }
                            k = (short)(k + 1);
                        }
                    }
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    // The decompile copies the item rect to a stack rect, then InsetRect(10,10)
                    // + {top += 10; bottom -= 10; right -= 5} before flowing the text into it.
                    insetRect[0] = itemRect[0]; insetRect[1] = itemRect[1];
                    insetRect[2] = itemRect[2]; insetRect[3] = itemRect[3];
                    MacToolbox.InsetRect(insetRect, 10, 10);
                    insetRect[0] = (short)(insetRect[0] + 10);   // top += 10
                    insetRect[2] = (short)(insetRect[2] - 10);   // bottom -= 10
                    insetRect[3] = (short)(insetRect[3] - 5);    // right -= 5
                    if (text.Length > 0)
                    {  // uppercase the first char via the Mac key table (faithful)
                        text = (char)(byte)LookupKeyTableShifted.Run(text[0]) + text.Substring(1);
                    }
                    text += ".";
                    MacToolbox.TETextBox(text, insetRect, 0);
                    MacToolbox.InvertRect(insetRect);
                    i = (short)(TotalMassWithEscorts.Run());
                    j = (short)(ShipDerivedStats.TotalMassCarried(ShipTable.Player));
                    i = (short)(i - j);
                    if (0 < i)
                    {
                        if (i < 0)
                        {
                            i = 0;
                        }
                        MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[2] + -14));
                        MacToolbox.RGBForeColor((uint)accentColor);
                        string freeStr = ((int)i).ToString();
                        j = (short)(ShipDerivedStats.EffectiveCargoMax());
                        k = (short)(TotalMassWithEscorts.Run());
                        if (j < k)
                        {
                            MacToolbox.DrawString("Free cargo space in fleet: ");
                            MacToolbox.ForeColor(QuickDrawColor.White);
                            MacToolbox.DrawString(freeStr);
                            MacToolbox.DrawString(" ton");
                            if (1 < i)
                            {
                                MacToolbox.DrawString("s");
                            }
                            short activeFighters = (short)(FreeCargoSpaceWithMissions.Run());
                            if (activeFighters < 0)
                            {
                                activeFighters = 0;
                            }
                            if ((activeFighters == i) || (activeFighters < 1))
                            {
                                if ((activeFighters == 0) && (0 < i))
                                {
                                    MacToolbox.DrawString(" (none free in your ship)");
                                }
                            }
                            else
                            {
                                MacToolbox.DrawString(" (");
                                MacToolbox.DrawString(((int)activeFighters).ToString());
                                MacToolbox.DrawString(" ton");
                                if (1 < activeFighters)
                                {
                                    MacToolbox.DrawString("s");
                                }
                                MacToolbox.DrawString(" free in your ship)");
                            }
                        }
                        else
                        {
                            MacToolbox.DrawString("Free cargo space: ");
                            MacToolbox.ForeColor(QuickDrawColor.White);
                            MacToolbox.DrawString(freeStr);
                            MacToolbox.DrawString(" ton");
                            if (1 < i)
                            {
                                MacToolbox.DrawString("s");
                            }
                        }
                    }
                }
            }

            // ── Page 3: extras (owned outfits) + trade-in value ──
            if (WorldState.PlayerInfoPage == 3)
            {
                i = 0;
                j = 0;
                while (true)
                {
                    if (j >= OwnedOutfitGrid.Count) break;
                    if (0 < OwnedOutfitGrid.Store[j])
                    {
                        i = (short)(i + 1);
                    }
                    j = (short)(j + 1);
                }
                if (i < 1)
                {
                    MacToolbox.RGBForeColor((uint)accentColor);
                    DrawCenteredString.Run("You don’t have any extras on your ship",
                                 (short)((int)itemRect[1]), (short)((int)itemRect[3]),
                                 (short)(itemRect[0] + 55));
                }
                else
                {
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[0] + 12));
                    MacToolbox.DrawString("Current extras for your ship:");
                    string text = "";
                    j = 0;
                    for (k = 0; k < OwnedOutfitGrid.Count; k = (short)(k + 1))
                    {
                        if (0 < OwnedOutfitGrid.Store[k])
                        {
                            if (OwnedOutfitGrid.Store[k] == 1)
                            {
                                // "a"/"an" by the (lowercased) first letter of the STR# 0x138c
                                // singular outfit name — decompile FUN_100760fc(*(name+1)), the
                                // first char of the Pascal string (ASCII for every outfit name).
                                string singularName = TextScratch.Trunc(ResourceGlobals.NamesStr138c[k], 29);
                                flag = (byte)(LookupKeyTableUnshifted.Run(singularName.Length > 0 ? (byte)singularName[0] : (byte)0));
                                if (flag == (byte)'a' || flag == (byte)'e' || flag == (byte)'i' ||
                                   flag == (byte)'o' || flag == (byte)'u')
                                {
                                    text += "an ";
                                }
                                else
                                {
                                    text += "a ";
                                }
                            }
                            else if (OwnedOutfitGrid.Store[k] == 2)
                            {
                                text += "two ";
                            }
                            else if (OwnedOutfitGrid.Store[k] == 3)
                            {
                                text += "three ";
                            }
                            else if (3 < OwnedOutfitGrid.Store[k])
                            {
                                text += ((int)OwnedOutfitGrid.Store[k]).ToString();
                                text += " ";
                            }
                            // Singular (STR# 0x138c) vs plural (0x138d) outfit name, max 29 chars.
                            if (OwnedOutfitGrid.Store[k] < 2)
                            {
                                text += TextScratch.Trunc(ResourceGlobals.NamesStr138c[k], 29);
                            }
                            else
                            {
                                text += TextScratch.Trunc(ResourceGlobals.NamesStr138d[k], 29);
                            }
                            j = (short)(j + 1);
                            if ((int)j < i + -1)
                            {
                                text += ", ";
                            }
                            if ((int)j == i + -1)
                            {
                                text += " and ";
                            }
                        }
                    }
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    insetRect[0] = itemRect[0]; insetRect[1] = itemRect[1];
                    insetRect[2] = itemRect[2]; insetRect[3] = itemRect[3];
                    MacToolbox.InsetRect(insetRect, 10, 10);
                    insetRect[0] = (short)(insetRect[0] + 10);   // top += 10
                    insetRect[2] = (short)(insetRect[2] - 10);   // bottom -= 10
                    insetRect[3] = (short)(insetRect[3] - 5);    // right -= 5
                    if (text.Length > 0)
                    {
                        text = (char)(byte)LookupKeyTableShifted.Run(text[0]) + text.Substring(1);
                    }
                    text += ".";
                    MacToolbox.TETextBox(text, insetRect, 0);
                    MacToolbox.InvertRect(insetRect);
                    MacToolbox.MoveTo((short)(itemRect[1] + 5), (short)(itemRect[2] + -14));
                    MacToolbox.RGBForeColor((uint)accentColor);
                    MacToolbox.DrawString("Ship trade-in value: ");
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    // The decompile drops the r3 carry-through: FUN_1005b4f8 (FormatCredits)
                    // runs with FUN_1005e948's (ship resale value) return still in r3, so the
                    // resale value is passed explicitly here.
                    FormatCredits.Run((int)ComputeShipResaleValue.Run());
                    MacToolbox.DrawString(" credits");
                }
            }
        }
        RenderPlayerInfoTabRow.Run(WorldState.PlayerInfoPage, -1);
        MacToolbox.RGBForeColor((uint)(UiColors.DialogFore));
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(PlayerInfoGlobals.DialogWindow);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // CopyBits(backdrop+2, win+2, win+0x10, win+0x10, 0, *(win+0x18)) — the
        // `+ 2` pixmap keys stay numeric (opaque registry keys); the rects/visRgn
        // go through the dialog accessors.
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2,
                            PlayerInfoGlobals.DialogWindow + 2,
                            MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow),
                            MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow), 0,
                            MacToolbox.GetDialogVisRgn(PlayerInfoGlobals.DialogWindow));
        return;
    }
}
