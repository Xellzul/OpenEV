using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_100578c8 (EV Override-11.c 35886-35939) — redraw the secondary-weapon HUD strip (the
// right-column band at y 237..254): "No Secondary Weapon", or the selected weapon's name, with
// " - <ammo>" appended when it carries countable ammo.
public static class RedrawHudWeaponPanel
{
    public static void Run()
    {
        // Mac Rects are {top, left, bottom, right} short[4]s.
        short[] dstRect = new short[4];
        short[] srcRect = new short[4];

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(MacFontId.Sillycon);
        MacToolbox.TextSize(14);
        MacToolbox.SetRect(dstRect, (short)(GlobalState.PortRight - 138), (short)(GlobalState.PortTop + 237),
                           (short)(GlobalState.PortRight - 6), (short)(GlobalState.PortTop + 254));
        MacToolbox.SetRect(srcRect, 6, 237, 138, 254);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2, srcRect, dstRect, 0, 0);

        short playerWeaponSlot = GameData.Player.SelectedWeaponSlot;
        if (playerWeaponSlot == -1)
        {
            MacToolbox.RGBForeColor((uint)UiColors.Friendly);
            DrawCenteredString.Run("No Secondary Weapon", dstRect[1], dstRect[3], (short)(dstRect[0] + 12));
        }
        else
        {
            MacToolbox.RGBForeColor((uint)UiColors.Neutral);
            var weapon = GameData.Weapons[playerWeaponSlot];
            string name = WeaponNameBuffer.Names[playerWeaponSlot];
            if (weapon.AmmoLink == -1)
            {
                DrawCenteredString.Run(name, dstRect[1], dstRect[3], (short)(dstRect[0] + 12));
            }
            else if (weapon.AmmoLink < -999)
            {
                // Original redundant re-test (always true here) — preserved for parity.
                if (weapon.AmmoLink < -999)
                    DrawCenteredString.Run(name, dstRect[1], dstRect[3], (short)(dstRect[0] + 12));
            }
            else
            {
                string label = name + " - " + GameData.Player.WeaponSlotAmmo[playerWeaponSlot];
                DrawCenteredString.Run(label, dstRect[1], dstRect[3], (short)(dstRect[0] + 12));
            }
        }

        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, dstRect, dstRect, 0, 0);
    }
}
