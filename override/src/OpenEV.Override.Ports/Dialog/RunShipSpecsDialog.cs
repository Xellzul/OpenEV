using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1003c5f0 (EV Override-11.c lines 24753-24835) — the shipyard SHIP-SPECS
// sub-dialog (DLOG 0x3ed), opened from the shipyard's Specs button / a grid
// double-click. Modal loop: item 1 = OK; 4 = galaxy map (RunGalaxyMapDialog);
// 2 = player info; 6 = mission info (only when a mission is active). The
// window lives in ShipyardState.SpecsDialogWindow (*_DAT_10080fec);
// the filter UPP cell (_DAT_10080ff0) holds FUN_1003c864
// (PictureDialogFilter, which redraws via DrawShipyardInfoDialog).
// ("ShowMissionsSubDialog" was an early transcription misname.)
//
// Dialog 4-rules rewrite: the dialog ptr-of-ptr cell routes through the
// managed ShipyardState.SpecsDialogWindow; win+0x10 InvalRects go through
// GetDialogPortRect; the per-govt mission-active byte reads the typed
// MissionStateTable.Store. The argument is ignored by the original
// FUN_1003c5f0(void) (a PPC extra-arg), so Run takes and discards it.
public static class RunShipSpecsDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10080ff0 → FUN_1003c864 =
    // PictureDialogFilter) — typed MacEvent shape (B10).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = PictureDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static void Run(int unusedSelectedClass)
    {
        _ = unusedSelectedClass;   // callers pass (int)SelectedRow; the original ignores it
        bool done = false;
        short hitItem = 0;   // local_42[0]
        int filterUpp = MacToolbox.NewRoutineDescriptor(ShipyardState.SpecsFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(ShipyardState.SpecsFilterProc, FilterAdapter);
        ShipyardState.SpecsDialogWindow = 0;
        ShipyardState.SpecsDialogWindow = MacToolbox.GetNewDialog(0x3ed, 0, -1);
        if (ShipyardState.SpecsDialogWindow != 0)
        {
            NewDialogHook.Run(ShipyardState.SpecsDialogWindow, 0);              // FUN_100583c4
            RecenterWindowIntoPlayArea.Run(ShipyardState.SpecsDialogWindow);  // FUN_100583c8
            MacToolbox.ShowWindow(ShipyardState.SpecsDialogWindow);
            MacToolbox.SelectWindow(ShipyardState.SpecsDialogWindow);
            MacToolbox.SetPort(ShipyardState.SpecsDialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.SpecsDialogWindow));   // win+0x10
            do
            {
                MacToolbox.ModalDialog(filterUpp, ref hitItem);
                if (hitItem == 1)
                {
                    done = true;
                }
                if (hitItem == 4)
                {
                    short savedSpob = GameData.Player.NavTargetSpob;   // *(ship+0x2c)
                    RunGalaxyMapDialog.Run();                             // FUN_10030014
                    GameData.Player.NavTargetSpob = savedSpob;
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);  // **PTR_DAT_10080ba0
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();                        // FUN_10037bb4
                    MacToolbox.SetPort(ShipyardState.SpecsDialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.SpecsDialogWindow));
                }
                if (hitItem == 2)
                {
                    RunPlayerInfoDialog.Run();                          // FUN_1003eda8
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(ShipyardState.SpecsDialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.SpecsDialogWindow));
                }
                if (hitItem == 6)
                {
                    short activeCount = 0;
                    for (short g = 0; g < MissionStateTable.Count; g = (short)(g + 1))
                    {
                        // *(char *)(_DAT_1008a544 + g*0x12) — per-govt mission-active byte.
                        if (GameData.MissionStates[g].IsActive != 0)
                        {
                            activeCount = (short)(activeCount + 1);
                        }
                    }
                    if (0 < activeCount)
                    {
                        RunMissionInfoDialog.Run();                     // FUN_1004fa88
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                        RedrawSpaceportDialog.Run();
                        MacToolbox.SetPort(ShipyardState.SpecsDialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.SpecsDialogWindow));
                    }
                }
            } while (!done);
            MacToolbox.DisposeRoutineDescriptor(filterUpp);
            MacToolbox.DisposeDialog(ShipyardState.SpecsDialogWindow);
            RepaintGameWindow.Run();                                  // FUN_1005ff4c
        }
        return;
    }
}
