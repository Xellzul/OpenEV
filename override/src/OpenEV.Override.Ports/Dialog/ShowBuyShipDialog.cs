using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1001312c (EV Override-11.c lines 9544-9667) — the buy/sell HAGGLE
// dialog (DLOG 0x3f0, the two-button dialog family shared with game-speed /
// comm-status). sellMode 0 = buying (button PICTs 0x1b94.., BuyShipMode 2),
// nonzero = selling (PICTs 0x1b90.., mode 1). A hidden rng(100) <= hagglePct
// roll decides whether the counterparty accepts ONE haggle (item 2): accept
// re-prices GameData.BuyShipPriceCell ×1.33 (selling) / ×0.75 (buying) and
// rounds down to 100s; refuse returns 2. Item 1 = done. Returns 0 = dialog
// failed to open, 1 = concluded, 2 = haggle refused.
//
// Dialog 4-rules rewrite: the filter UPP cell read (0x10080cf8 →
// FUN_100134dc) is the named DialogScratch.TwoButtonFilterProc; the price
// multipliers are dumped data-seg literals and the int->double pack idiom
// collapses to a plain (double) cast. The decompile's window/dialog-centre
// dead stores (local_52/54/5a/5c and the local_58/56 copies, never read) are
// dropped.
public static class ShowBuyShipDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10080cf8 -> FUN_100134dc) —
    // typed MacEvent shape (dialog 4-rules B10).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = TwoButtonDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static short Run(byte sellMode, short hagglePct)
    {
        bool done = false;
        // NewRoutineDescriptor(_DAT_10080cf8, 0xfd0, 1) — the PEF-relocated UPP
        // cell holds FUN_100134dc (TwoButtonDialogFilter).
        int routineDesc = MacToolbox.NewRoutineDescriptor(DialogScratch.TwoButtonFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(DialogScratch.TwoButtonFilterProc, FilterAdapter);
        for (short i = 0; i < DialogScratch.BuyShipPicts.Length; i = (short)(i + 1))
        {
            if (sellMode == 0)
            {
                DialogScratch.BuyShipPicts[i] = MacToolbox.GetPicture(i + 0x1b94);
            }
            else
            {
                DialogScratch.BuyShipPicts[i] = MacToolbox.GetPicture(i + 0x1b90);
            }
        }
        if (sellMode == 0)
        {
            DialogScratch.BuyShipMode = 2;   // sRam10086acc
        }
        else
        {
            DialogScratch.BuyShipMode = 1;
        }
        short resultCode = 0;
        // FUN_1005d9c4(100): the haggle-acceptance roll.
        bool haggleOpen = (short)SeedEvoRng.Run(100) <= hagglePct;
        DialogScratch.BuyShipDialogRecord = 0;   // _DAT_10086ac4
        DialogScratch.BuyShipDialogRecord = MacToolbox.GetNewDialog(0x3f0, 0, -1);
        short returnCode = 0;
        if (DialogScratch.BuyShipDialogRecord != 0)
        {
            NewDialogHook.Run(DialogScratch.BuyShipDialogRecord, 0);              // FUN_100583c4
            RecenterWindowIntoPlayArea.Run(DialogScratch.BuyShipDialogRecord);  // FUN_100583c8
            MacToolbox.ShowWindow(DialogScratch.BuyShipDialogRecord);
            MacToolbox.SelectWindow(DialogScratch.BuyShipDialogRecord);
            MacToolbox.SetPort(DialogScratch.BuyShipDialogRecord);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            short hitItem = 0;   // local_5e
            do
            {
                MacToolbox.ModalDialog(routineDesc, ref hitItem);
                if (hitItem == 1)
                {
                    DrawGameSpeedDialogButtons.Run(-1);   // FUN_1000ea3c(0xffffffff)
                    if (resultCode != 2)
                    {
                        resultCode = 1;
                    }
                    done = true;
                }
                if (hitItem == 2)
                {
                    if (haggleOpen)
                    {
                        // Re-price the cell: the data-seg doubles GameToc-0x69d8 =
                        // 0x10081c88 (1.33), -0x69e0 = 0x10081c80 (0.75) and -0x6980 =
                        // 0x10081ce0 (0.01) are dumped literals; -0x69c0 = 0x10081ca0 is
                        // the standard i2d bias, so the pack+bias idiom = a plain
                        // (double) cast of the int price cell.
                        if (DialogScratch.BuyShipMode == 1)
                        {
                            GameData.BuyShipPriceCell =
                                (int)((double)GameData.BuyShipPriceCell * 1.33);
                        }
                        if (DialogScratch.BuyShipMode == 2)
                        {
                            GameData.BuyShipPriceCell =
                                (int)((double)GameData.BuyShipPriceCell * 0.75);
                        }
                        // Round down to whole hundreds.
                        GameData.BuyShipPriceCell =
                            (int)((double)GameData.BuyShipPriceCell * 0.01) * 100;
                        RedrawCommStatusLine.Run();   // FUN_10013614
                        haggleOpen = false;
                        resultCode = 1;
                    }
                    else
                    {
                        resultCode = 2;
                        done = true;
                    }
                    DrawGameSpeedDialogButtons.Run(-1);
                }
            } while (!done);
            for (short i = 0; i < DialogScratch.BuyShipPicts.Length; i = (short)(i + 1))
            {
                if (DialogScratch.BuyShipPicts[i] != 0)
                {
                    MacToolbox.HPurge(DialogScratch.BuyShipPicts[i]);
                    MacToolbox.ReleaseResource(DialogScratch.BuyShipPicts[i]);
                }
            }
            MacToolbox.DisposeRoutineDescriptor(routineDesc);
            // (The decompile's dialog/window centre computations — local_52/54 from
            // the dialog rect, local_5a/5c from the render-ctx portRect, plus the
            // local_58/56 copies — are dead stores; dropped.)
            MacToolbox.DisposeDialog(DialogScratch.BuyShipDialogRecord);
            RepaintGameWindow.Run();   // FUN_1005ff4c
            returnCode = resultCode;
        }
        return returnCode;
    }
}
