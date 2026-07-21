using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Systems;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10062638 (EV Override-11.c lines 41302-41351).
public static class Tick
{
    public static void Run(byte fullUpdate)
    {
        HideCursorOnce.Run();
        SpawnWorldSpriteNodes.Run();
        Keymap.RefreshCachedKeymap();
        TickShipAI.Run(ShipTable.Player);
        if (fullUpdate != 0)
        {
            TickHudRedrawScheduler.Run();
            TickAllMissions.Run();
            TickAmbientSoundChannel.Run();
            junkcode.FUN_10023060();
            TickFlashEffectCountdown.Run();
            AccumulateIncomingDamageThreat.Run();
            SpawnFleetShips.Run((int)GameData.Player.CurrentSystem);
            Asteroids.Tick(1);
            WorldState.NpcScanningPlayer = 0;
            for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex = (short)(shipIndex + 1))
            {
                var ship = ShipTable.Ships[shipIndex];
                if (ship.IsActive != 0)
                {
                    if (0 < ship.AiBehaviorType)
                    {
                        if (GameData.Player.CurrentSystem == ship.CurrentSystem)
                        {
                            ShipAi.DispatchAi(ship);
                        }
                    }
                }
            }
        }
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex = (short)(shipIndex + 1))
        {
            var ship = ShipTable.Ships[shipIndex];
            if (ship.IsActive != 0)
            {
                if (GameData.Player.CurrentSystem == ship.CurrentSystem)
                {
                    UpdateShipAiFrame.Run(ship);
                }
            }
        }
        if (fullUpdate != 0)
        {
            UpdateProjectilePositions.Run();
        }
        return;
    }
}
