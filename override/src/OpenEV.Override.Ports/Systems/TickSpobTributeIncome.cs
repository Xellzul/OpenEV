using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_1006c044 (EV Override-11.c lines 44393-44410): accrues a tribute
// tick and pays the player TechLevel * 1000 credits for every visible,
// trading-enabled spob.
public static class TickSpobTributeIncome
{
    public static void Run()
    {
        foreach (var spob in GameData.Spobs)
        {
            if (spob.Visible != 0 && spob.TradingEnabled != 0)
            {
                spob.TributeAccrualTicks = (short)(spob.TributeAccrualTicks + 1);
                GameData.Player.Credits = GameData.Player.Credits + spob.TechLevel * 1000;
                WorldState.HudStatusPanelDirty = 1;
            }
        }
    }
}
