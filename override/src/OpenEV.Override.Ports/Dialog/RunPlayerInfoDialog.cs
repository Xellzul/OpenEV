using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Outfit;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1003eda8 (EV Override-11.c lines 25856-25936) — the in-game PLAYER INFO
// dialog (DLOG 0x3f9 = 1017; "ShowDomainCaptureDialog" was an early transcription misname).
// Opens only when the player ship is idle (JumpWindupTimer == 0, speed <= 0 and no
// cinematic flag set); items 2..5 switch WorldState.PlayerInfoPage 1..4
// (stats / cargo / extras / capture — RenderPlayerInfoDialog draws the page),
// item 1 leaves. Reached from the hub's item 2, every sub-dialog's info
// button and the in-game info key.
public static class RunPlayerInfoDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10080fd8 -> FUN_1003f044) — typed
    // MacEvent shape.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = PlayerInfoDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static void Run()
    {
        bool done = false;
        short hitItem = default;

        int filterUpp = MacToolbox.NewRoutineDescriptor(PlayerInfoGlobals.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(filterUpp, FilterAdapter);
        // Compares float VALUES here, not raw bit patterns — the *(toc-0x6660)
        // threshold is 0.0f, so DeathTimer <= 0f means the ship must be stopped.
        if ((((GameData.Ships[0].JumpWindupTimer == 0) &&
             (GameData.Ships[0].DeathTimer <= 0.0f)) &&
            (WorldState.UiSuppressGateA == 0)) && (WorldState.UiSuppressGateB == 0))
        {
            RebuildOwnedOutfitsFromMarket.Run();
            WorldState.PlayerInfoPage = 1;
            PlayerInfoGlobals.Picts[0] = MacToolbox.GetPicture(0x1b64);
            PlayerInfoGlobals.Picts[1] = MacToolbox.GetPicture(0x1b65);
            PlayerInfoGlobals.Picts[2] = MacToolbox.GetPicture(0x1b7e);
            PlayerInfoGlobals.Picts[3] = MacToolbox.GetPicture(0x1b7f);
            PlayerInfoGlobals.Picts[4] = MacToolbox.GetPicture(0x1b84);
            PlayerInfoGlobals.Picts[5] = MacToolbox.GetPicture(0x1b85);
            PlayerInfoGlobals.Picts[6] = MacToolbox.GetPicture(0x1b86);
            PlayerInfoGlobals.Picts[7] = MacToolbox.GetPicture(0x1b87);
            PlayerInfoGlobals.DialogWindow = 0;
            PlayerInfoGlobals.DialogWindow = MacToolbox.GetNewDialog(0x3f9, 0, -1); // -1 = front window
            if (PlayerInfoGlobals.DialogWindow != 0)
            {
                NewDialogHook.Run(PlayerInfoGlobals.DialogWindow, 0);
                RecenterWindowIntoPlayArea.Run(PlayerInfoGlobals.DialogWindow);
                MacToolbox.ShowWindow(PlayerInfoGlobals.DialogWindow);
                MacToolbox.SelectWindow(PlayerInfoGlobals.DialogWindow);
                MacToolbox.SetPort(PlayerInfoGlobals.DialogWindow);
                MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
                MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow));
                do
                {
                    MacToolbox.ModalDialog(filterUpp, ref hitItem);
                    if (hitItem == 1)
                    {
                        done = true;
                    }
                    if ((1 < hitItem) && (hitItem < 6))
                    {
                        WorldState.PlayerInfoPage = (short)(hitItem - 1); // items 2..5 -> pages 1..4
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow));
                    }
                } while (!done);
                // Original-game bug kept: only 8 PICT handles are loaded but the purge
                // loop walks 10 entries.
                for (short index = 0; index < 10; index = (short)(index + 1))
                {
                    if (PlayerInfoGlobals.Picts[index] != 0)
                    {
                        MacToolbox.HPurge(PlayerInfoGlobals.Picts[index]);
                        MacToolbox.ReleaseResource(PlayerInfoGlobals.Picts[index]);
                    }
                }
                SetGamePortAndDevice.Run();
                MacToolbox.DisposeRoutineDescriptor(filterUpp);
                MacToolbox.DisposeDialog(PlayerInfoGlobals.DialogWindow);
                SetGamePortAndDevice.Run();
                RepaintGameWindow.Run();
            }
        }
    }
}
