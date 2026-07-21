namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;

// Port of FUN_1005d52c (EV Override-11.c 38706-38727) — disengage the cloaking device: play the
// de-cloak sound, restore the active HUD colours + the default screen palette (drops the cloak
// tint), mark the HUD layers dirty, drop the IsCloaked flag, and refresh the status panel.
// No-op when not cloaked.
public static class DisengageCloaking
{
    public static void Run()
    {
        if (!WorldState.IsCloaked)
            return;

        SndPlay.Run(SoundResourceCells.CloakDisengageSnd, 8, 128, 128);
        Palette.SetHudColorsActive();
        WorldState.HudWeaponPanelDirty = 1;
        WorldState.SpawnPulseDirty = 1;
        WorldState.ShieldEnergyBarDirty = 1;
        WorldState.PlayerShieldBarDirty = 1;
        TickHudRedrawScheduler.Run();
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 1);
        WorldState.IsCloaked = false;
        RefreshStatusPanel.Run();
    }
}
