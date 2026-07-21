using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1006bce4 (EV Override-11.c lines 44315-44387). Each pay tick, charge the
// player 1% of every carried-fighter escort's class cost; an escort that can't be paid
// defects (its fleader's escorts redistribute fuel first). Announces defections once.
public static class PayEscortWages
{
    public static void Run(short passCount)
    {
        short defectedCount = 0;
        for (short pass = 0; pass < passCount; pass++)
        {
            for (short escortIndex = 1; escortIndex < ShipTable.Count; escortIndex++)
            {
                var escort = ShipTable.Ships[escortIndex];
                if (escort.IsActive == 0 || escort.OwnerSlot != 0 ||
                    escort.AiBehaviorType != ShipAiType.Escort || ShipDerivedStats.IsDisabled(escort))
                    continue;

                // A grudge mission with a protect-player ShipBehav (units digit 1) exempts the escort.
                bool isExempt = false;
                if (escort.GrudgeMissionIndex != -1 &&
                    GameData.MissionStates[escort.GrudgeMissionIndex].IsActive != 0)
                {
                    short shipBehavior = GameData.Missions[escort.GrudgeMissionIndex].ShipBehavior;
                    for (; shipBehavior > 8; shipBehavior -= 10) { }
                    isExempt = shipBehavior == 1;
                }
                if (isExempt || escort.IsCarriedFighter == 0)
                    continue;

                int wage = (int)(MathConstants.OnePercent *
                                 (double)GameData.ShipClasses[escort.ShipClass].Cost);
                var player = ShipTable.Player;
                if (player.Credits < wage)
                {
                    defectedCount++;
                    if (escort.AiBehaviorType == ShipAiType.Escort && escort.GrudgeMissionIndex == -1 &&
                        GameData.ShipClasses[escort.ShipClass].InherentAI < ShipAiType.Warship)
                        RedistributeCargoAmongShips.Run(escortIndex);
                    WorldState.HudStatusPanelDirty = 1;
                    escort.IsActive = 0;
                    escort.HasWorldSpriteNode = 0;
                    escort.OwnerSlot = -1;
                    escort.AiBehaviorType = ShipAiType.WimpyTrader;
                }
                else
                {
                    player.Credits -= wage;
                    WorldState.HudStatusPanelDirty = 1;
                }
            }
        }
        if (defectedCount > 0)
        {
            AlertText.Message = defectedCount == 1
                ? "Due to lack of pay, one of your escorts has defected."
                : "Due to lack of pay, some of your escorts have defected.";
            WorldState.HudStatusPanelDirty = 1;
            DoSceneTransition.Run(0, 0);
            RepaintGameWindow.Run();
            TickHudRedrawScheduler.Run();
        }
    }
}
