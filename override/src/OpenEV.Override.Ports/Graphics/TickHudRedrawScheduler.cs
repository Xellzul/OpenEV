using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100551f8 (EV Override-11.c lines 34884-34971): the per-frame in-game HUD redraw
// scheduler. Sets dirty flags on a cadence (target/radar timers, the every-15th anim step), then
// drives each HUD element's redraw from its dirty flag.
public static class TickHudRedrawScheduler
{
    public static void Run()
    {
        if (WorldState.UiSuppressGateA != 0 || WorldState.UiSuppressGateB != 0)   // HUD suppressed
            return;

        if (GameData.Player.NavMode != -1 && MacToolbox.TickCount() % 30 == 0)
            WorldState.SpawnPulseDirty = 1;
        if (GameData.Player.TargetSlot != -1 && MacToolbox.TickCount() % 45 == 0)
        {
            WorldState.HudStatusPanelDirty = 1;
            WorldState.WeaponSlotDirty = 1;
        }

        byte jamFlag = (byte)(Keymap.TestCachedKeymapBit(0x33) != 0 ? 1 : 0);
        RenderGlobals.RadarHudJamFlag = jamFlag;

        short tickCounter = (short)(RenderGlobals.RadarHudAnimTick + 1);
        RenderGlobals.RadarHudAnimTick = tickCounter;
        if (tickCounter % 15 == 0)
        {
            if (30 < tickCounter)
            {
                tickCounter = 0;
                RenderGlobals.RadarHudAnimTick = tickCounter;
                short target = GameData.Player.TargetSlot;
                if (target == -1)
                {
                    // No target: reset the two HUD caches to the force-redraw sentinel.
                    RenderGlobals.HudCachedTargetShield = unchecked((short)0x8001);
                    RenderGlobals.HudCachedTargetClass = unchecked((short)0x8001);
                }
                else
                {
                    // Redraw the weapon panel when the cached (short) target shield / class or the
                    // cached key flag no longer matches the live target. The shield compare is the
                    // cached value vs the live (int)Shield value (+0x68 holds the int shield).
                    if ((int)RenderGlobals.HudCachedTargetShield != (int)GameData.Ships[target].Shield ||
                        RenderGlobals.HudCachedTargetClass != GameData.Ships[target].ShipClass)
                    {
                        WorldState.WeaponSlotDirty = 1;
                    }
                    if (RenderGlobals.HudCachedJamFlag != jamFlag || jamFlag != 0)
                        WorldState.WeaponSlotDirty = 1;
                }
            }
            WorldState.RadarRedrawDirty = 1;
        }

        if (WorldState.WeaponSlotDirty != 0)
        {
            DrawTargetInfoPanel.Run();
            WorldState.WeaponSlotDirty = 0;
        }
        if (WorldState.RadarRedrawDirty != 0)
        {
            DrawRadarHud.Run(0);
            WorldState.RadarRedrawDirty = 0;
        }
        if (WorldState.HudWeaponPanelDirty != 0)
        {
            RedrawHudWeaponPanel.Run();
            WorldState.HudWeaponPanelDirty = 0;
        }
        if (WorldState.SpawnPulseDirty != 0)
        {
            DrawTargetShipInfoPanel.Run();
            WorldState.SpawnPulseDirty = 0;
        }
        if (WorldState.PlayerShieldBarDirty != 0)
        {
            DrawPlayerShieldBar.Run();
            WorldState.PlayerShieldBarDirty = 0;
        }
        if (WorldState.ShieldEnergyBarDirty != 0)
        {
            DrawShieldEnergyBar.Run();
            WorldState.ShieldEnergyBarDirty = 0;
        }
        if (WorldState.HudStatusPanelDirty != 0)
        {
            RedrawHudStatusPanel.Run();
            WorldState.HudStatusPanelDirty = 0;
        }
    }
}
