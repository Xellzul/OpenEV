// Port of FUN_1006f2c8 (EV Override-11.c lines 45350-45573).
//
// The shareware-nag bank-robbery news event, fired on landing when the pilot is
// unregistered with 300k+ credits: Cap'n Hector "robs" 20-40% of the credits and
// posts a ransom-note news item.

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Text;

public static class BankRobberyNewsEvent
{
    public static void Run()
    {
        if (WorldState.SharewareRegisteredMatch != 0 || ShipTable.Credits(0) < 300000)
        {
            return;
        }
        int oddsDenom;
        if (ShipTable.Credits(0) < 1500000)
        {
            oddsDenom = ShipTable.Credits(0) < 800000 ? 100 : 50;
        }
        else
        {
            oddsDenom = 25;
        }
        // 1-in-oddsDenom chance to proceed (the != 0 miss returns).
        if ((short)SeedEvoRng.Run((short)oddsDenom) != 0)
        {
            return;
        }
        short roll = (short)SeedEvoRng.Run(3);
        int amount = (int)((double)ShipTable.Credits(0) * (0.2 + (double)roll / 10.0));
        if (amount < 1000)
        {
            return;
        }
        if (999999999 < amount)
        {
            amount = 999999999;
        }
        string news = (short)SeedEvoRng.Run(3) switch
        {
            0 => "The shareware crusader Cap’n Hector ",
            1 => "The infamous shareware crusader Cap’n Hector ",
            _ => "Cap’n Hector, the notorious shareware crusader, ",
        };
        news += (short)SeedEvoRng.Run(2) == 0 ? "robbed " : "held up ";
        news += (short)SeedEvoRng.Run(3) switch
        {
            0 => "the First Interstellar Bank ",
            1 => "the First Bank of Regulus ",
            _ => "the Arcturan Savings & Loan ",
        };
        news += "today, ";
        news += (short)SeedEvoRng.Run(2) == 0 ? "making off with " : "stealing ";
        // Manual digit grouping into "M,NNN,000" with zero-padded thousands.
        int truncated = (amount / 1000) * 1000;
        int millions = 0;
        if (1000000 < truncated)
        {
            millions = truncated / 1000000;
        }
        int thousands = (truncated - millions * 1000000) / 1000;
        if (0 < millions)
        {
            news += millions.ToString();
            news += ",";
            if (thousands < 100)
            {
                news += "0";
            }
            if (thousands < 10)
            {
                news += "0";
            }
        }
        news += thousands.ToString();
        news += ",000 of your credits! Authorities have stated that the only way to end this feathered fiend’s reign of terror is for you to register EV Override.";
        AlertText.Message = news;
        ShipTable.SetCredits(0, ShipTable.Credits(0) - truncated);
        WorldState.HudStatusPanelDirty = 1;
        TickHudRedrawScheduler.Run();
        SndPlay.Run(CombatSoundCells.AlarmSnd, 5, 128, 128);
        RunAboutDialog.Run();
        RepaintGameWindow.Run();
    }
}
