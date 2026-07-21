using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1006f150 (EV Override-11.c lines 45310-45349).
public static class TickPersNagHook
{
    public static void Run()
    {
        bool shouldNag = false;
        short tradeableSpobCount = 0;
        for (short spobIndex = 0; spobIndex < SpobTable.Count; spobIndex = (short)(spobIndex + 1))
        {
            if (GameData.Spobs[spobIndex].Visible != 0 &&
                GameData.Spobs[spobIndex].TradingEnabled != 0)
            {
                tradeableSpobCount = (short)(tradeableSpobCount + 1);
            }
        }
        if (tradeableSpobCount == 1 && SeedEvoRng.Run(10) == 0)
        {
            shouldNag = true;
        }
        if (1 < tradeableSpobCount)
        {
            if (SeedEvoRng.Run(5) == 0)
            {
                shouldNag = true;
            }
        }
        short spawnedSlot;
        if (shouldNag && GameData.Pers[ShipRecord.EngagePlayerPersIndex].AvailableFlag != 0 &&
            (spawnedSlot = (short)SpawnPers.Run((int)GameData.Player.CurrentSystem, 1, ShipRecord.EngagePlayerPersIndex)) != -1)
        {
            WorldState.FlashChatterCountdown = -1;   // short sentinel (0xffff)
            ShipAi.CallForDefendersAndEngagePlayer(ShipTable.Ships[spawnedSlot]);
            WorldState.CurrentTargetShipId = spawnedSlot;
            SpeakPersHailLine.Run((int)GameData.Pers[GameData.Ships[spawnedSlot].PersIndex].HailQuote);
            WorldState.CurrentTargetShipId = -1;
        }
    }
}
