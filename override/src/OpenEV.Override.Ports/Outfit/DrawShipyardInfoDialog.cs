using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1003cad0 (EV Override-11.c lines 24935-25294) — the ship-specs sub-dialog
// (DLOG 0x3ed, RunShipSpecsDialog) redraw. Draws the class-name header (item 3,
// Times 18), the stat sheet (item 5: speed, accel/turn grades, shield, armor,
// guns, turrets, free space, cargo, fuel jumps, standard-weapons list, plus the
// length/mass/crew column), and the OK-button PICT (item 1, ShipyardState.Picts[4])
// into the BACKDROP GWorld, then CopyBits the lot onto the dialog window. Invoked
// by the specs filter (Dialog.PictureDialogFilter, FUN_1003c864) on updateEvt.
public static class DrawShipyardInfoDialog
{
    public static void Run()
    {
        int dialog = ShipyardState.SpecsDialogWindow;
        int labelColor = UiColors.DialogFore;
        var itemKind = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];   // {top, left, bottom, right}

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(dialog));
        MacToolbox.RGBForeColor((uint)labelColor);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(dialog));
        MacToolbox.ForeColor(QuickDrawColor.Black);

        // ── Item 3: class-name header ─────────────────────────────────────
        MacToolbox.GetDialogItem(dialog, 3, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dialog)))
        {
            MacToolbox.TextFont(20);   // Times
            MacToolbox.TextSize(18);
            MacToolbox.MoveTo(itemRect[1], itemRect[0] + 18);
            MacToolbox.ForeColor(QuickDrawColor.White);
            DrawCenteredString.Run(
                GameData.ShipClasses[ShipyardState.SelectedRow].Name,
                itemRect[1], itemRect[3], (short)(itemRect[0] + 18));
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        // ── Item 5: the stat sheet ────────────────────────────────────────
        MacToolbox.GetDialogItem(dialog, 5, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dialog)))
        {
            var cls = GameData.ShipClasses[ShipyardState.SelectedRow];
            short top = itemRect[0], left = itemRect[1];
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            // Speed row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 12);
            MacToolbox.DrawString("Speed:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 12);
            int statValue = (int)(100.0 * cls.Speed);   // *(toc-0x6628) = 100.0
            MacToolbox.DrawString(statValue.ToString());
            // Accel grade row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 24);
            MacToolbox.DrawString("Accel:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 24);
            MacToolbox.DrawString(AccelGradeLabel(cls.Accel));
            // Turn grade row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 36);
            MacToolbox.DrawString("Turn:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 36);
            MacToolbox.DrawString(TurnGradeLabel(cls.Maneuver));
            // Shield row: display = 10% of the internal value.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 48);
            MacToolbox.DrawString("Shields:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 48);
            if (cls.Shield < 1)
            {
                MacToolbox.DrawString("None");
            }
            else
            {
                statValue = (int)(0.1 * cls.Shield);   // *(toc-0x6620) = 0.1
                FormatCredits.Run(statValue);
            }
            // Armor row: same 10% display scale.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 60);
            MacToolbox.DrawString("Armor:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 60);
            if (cls.BaseArmor < 1)
            {
                MacToolbox.DrawString("None");
            }
            else
            {
                statValue = (int)(0.1 * cls.BaseArmor);
                FormatCredits.Run(statValue);
            }
            // Guns row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 72);
            MacToolbox.DrawString("Guns:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 72);
            if (cls.MaxGun < 1)
            {
                MacToolbox.DrawString("None");
            }
            else
            {
                MacToolbox.DrawString("Maximum of ");
                MacToolbox.DrawString(((int)cls.MaxGun).ToString());
            }
            // Turrets row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 84);
            MacToolbox.DrawString("Turrets:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 84);
            if (cls.MaxTur < 1)
            {
                MacToolbox.DrawString("None");
            }
            else
            {
                MacToolbox.DrawString("Maximum of ");
                MacToolbox.DrawString(((int)cls.MaxTur).ToString());
            }
            short freeSpace = ComputeShipyardFreeSpace(cls);
            // Space row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 96);
            MacToolbox.DrawString("Space:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 96);
            MacToolbox.DrawString(((int)freeSpace).ToString());
            MacToolbox.DrawString(" ton");
            if (freeSpace != 1)
            {
                MacToolbox.DrawString("s");
            }
            // Cargo row.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 108);
            MacToolbox.DrawString("Cargo:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 108);
            MacToolbox.DrawString(((int)cls.Holds).ToString());
            MacToolbox.DrawString(" ton");
            if (cls.Holds != 1)
            {
                MacToolbox.DrawString("s");
            }
            // Fuel row (jumps = BaseFuel / 100).
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left, top + 120);
            MacToolbox.DrawString("Fuel:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 45, top + 120);
            MacToolbox.DrawString(((int)cls.BaseFuel / 100).ToString());
            MacToolbox.DrawString(" jump");
            if (100 < cls.BaseFuel)
            {
                MacToolbox.DrawString("s");
            }
            // Standard-weapons list.
            short stdWeaponCount = 0;
            for (short w = 0; w < ShipClassRecord.WeaponSlotCount; w = (short)(w + 1))
            {
                if (0 < cls.DefaultWeaponType[w])
                {
                    stdWeaponCount = (short)(stdWeaponCount + 1);
                }
            }
            if (stdWeaponCount == 0)
            {
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo(left, top + 144);
                MacToolbox.DrawString("No standard weapons");
            }
            else
            {
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo(left, top + 144);
                MacToolbox.DrawString("Standard weapons:");
                int lineY = top + 156;
                for (short w = 0; w < ShipClassRecord.WeaponSlotCount; w = (short)(w + 1))
                {
                    if (0 < cls.DefaultWeaponType[w])
                    {
                        MacToolbox.MoveTo(left + 15, lineY);
                        if (GameData.Weapons[w].GuidanceType == 99)
                        {
                            // Guidance 99: list the AMMO count + ammo outfit name only.
                            MacToolbox.DrawString(((int)cls.DefaultWeaponAmmo[w]).ToString());
                            MacToolbox.DrawString(" ");
                            foreach (var outfit in OutfitTable.Store)
                            {
                                if (outfit.ModType[0] == OutfitModType.Ammo && w == outfit.ModValue[0])
                                {
                                    MacToolbox.DrawString(outfit.Name);
                                    break;
                                }
                            }
                            if (1 < cls.DefaultWeaponAmmo[w])
                            {
                                MacToolbox.DrawString("s");
                            }
                        }
                        else
                        {
                            MacToolbox.DrawString(((int)cls.DefaultWeaponType[w]).ToString());
                            MacToolbox.DrawString(" ");
                            foreach (var outfit in OutfitTable.Store)
                            {
                                if (outfit.ModType[0] == OutfitModType.Weapon && w == outfit.ModValue[0])
                                {
                                    MacToolbox.DrawString(outfit.Name);
                                    break;
                                }
                            }
                            if (1 < cls.DefaultWeaponType[w])
                            {
                                MacToolbox.DrawString("s");
                            }
                            if (0 < cls.DefaultWeaponAmmo[w])
                            {
                                MacToolbox.DrawString(" + ");
                                MacToolbox.DrawString(((int)cls.DefaultWeaponAmmo[w]).ToString());
                                MacToolbox.DrawString(" ");
                                foreach (var outfit in OutfitTable.Store)
                                {
                                    if (outfit.ModType[0] == OutfitModType.Ammo && w == outfit.ModValue[0])
                                    {
                                        MacToolbox.DrawString(outfit.Name);
                                        break;
                                    }
                                }
                                if (1 < cls.DefaultWeaponAmmo[w])
                                {
                                    MacToolbox.DrawString("s");
                                }
                            }
                        }
                        lineY = lineY + 12;
                    }
                }
            }
            // Right column: length / mass / crew.
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left + 100, top + 96);
            MacToolbox.DrawString("Length:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 138, top + 96);
            MacToolbox.DrawString(((int)cls.Length).ToString());
            MacToolbox.DrawString(" m");
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left + 100, top + 108);
            MacToolbox.DrawString("Mass:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 138, top + 108);
            MacToolbox.DrawString(((int)cls.Mass).ToString());
            MacToolbox.DrawString(" ton");
            if (cls.Mass != 1)
            {
                MacToolbox.DrawString("s");
            }
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.MoveTo(left + 100, top + 120);
            MacToolbox.DrawString("Crew:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(left + 138, top + 120);
            MacToolbox.DrawString(((int)cls.Crew).ToString());
        }
        // ── Item 1: the OK-button PICT ────────────────────────────────────
        MacToolbox.GetDialogItem(dialog, 1, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dialog)))
        {
            // The pressed mate (Picts[5]) is drawn separately, by PictureDialogFilter's tracking.
            MacToolbox.DrawPicture(ShipyardState.Picts[4], itemRect);
        }
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(dialog);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        var portRect = MacToolbox.GetDialogPortRect(dialog);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, dialog + 2, portRect, portRect, 0, 0);
    }

    // FUN_1003cad0 24999-25035 — accel grade label. Thresholds are the data-seg
    // doubles at GameToc-0x6630..-0x6658 (0x10082030..0x10082008, dumped).
    private static string AccelGradeLabel(float accel)
    {
        if (accel <= 0.073)
        {
            if (accel <= 0.055)
            {
                if (accel <= 0.038)
                {
                    if (accel <= 0.025)
                    {
                        if (accel <= 0.013)
                        {
                            if (accel <= 0.0)
                            {
                                return "N/A";
                            }
                            return "Terrible";
                        }
                        return "Poor";
                    }
                    return "Average";
                }
                return "Good";
            }
            return "Very Good";
        }
        return "Excellent";
    }

    // FUN_1003cad0 25041-25061 — turn grade label; same string set as
    // AccelGradeLabel, keyed on the integer Maneuver stat (0..5+) instead.
    private static string TurnGradeLabel(short maneuver)
    {
        if (maneuver < 1)
        {
            return "N/A";
        }
        if (maneuver == 1)
        {
            return "Terrible";
        }
        if (maneuver == 2)
        {
            return "Poor";
        }
        if (maneuver == 3)
        {
            return "Average";
        }
        if (maneuver == 4)
        {
            return "Good";
        }
        if (maneuver == 5)
        {
            return "Very Good";
        }
        return "Excellent";
    }

    // FUN_1003cad0 25120-25142 — free cargo space: class FreeMass minus the
    // mass of the default weapon + ammo loadout (matched against the outfit
    // table by ModType/ModValue).
    private static short ComputeShipyardFreeSpace(ShipClassRecord cls)
    {
        short freeSpace = cls.FreeMass;
        for (short w = 0; w < ShipClassRecord.WeaponSlotCount; w = (short)(w + 1))
        {
            if (0 < cls.DefaultWeaponType[w])
            {
                foreach (var outfit in OutfitTable.Store)
                {
                    if (outfit.ModType[0] == OutfitModType.Weapon && w == outfit.ModValue[0])
                    {
                        freeSpace = (short)(freeSpace - outfit.Mass * cls.DefaultWeaponType[w]);
                        break;
                    }
                }
            }
            if (0 < cls.DefaultWeaponAmmo[w])
            {
                foreach (var outfit in OutfitTable.Store)
                {
                    if (outfit.ModType[0] == OutfitModType.Ammo && w == outfit.ModValue[0])
                    {
                        freeSpace = (short)(freeSpace - outfit.Mass * cls.DefaultWeaponAmmo[w]);
                        break;
                    }
                }
            }
        }
        return freeSpace;
    }
}
