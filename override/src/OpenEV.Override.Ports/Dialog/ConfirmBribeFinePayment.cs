using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_100107d8 (EV Override-11.c lines 8556-8580) — spaceport-person
// bribe/fine payment confirm: shows the price-confirm dialog for the current
// bribe/fine amount. Can't afford → -1; confirmed → deduct the price and flag
// the status panel for redraw → 1; declined → fine += 1000 → 0.
public static class ConfirmBribeFinePayment
{
    public static int Run()
    {
        DrawOutfitterItemPanel.Run();

        GameData.BuyShipPriceCell = GameData.BribeFine;

        short confirm = ShowBuyShipDialog.Run(0, 50);
        MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);

        ShipRecord player = GameData.Player;
        // read the post-dialog price cell, not BribeFine — ShowBuyShipDialog can
        // re-price the cell ×0.75 on a successful haggle
        if (player.Credits < GameData.BuyShipPriceCell)
            return -1;

        if (confirm == 1)
        {
            player.Credits -= GameData.BuyShipPriceCell;
            WorldState.HudStatusPanelDirty = 1;
            return 1;
        }

        GameData.BribeFine += 1000;
        return 0;
    }
}
