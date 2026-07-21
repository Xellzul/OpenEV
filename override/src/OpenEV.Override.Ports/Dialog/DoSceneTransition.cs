using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003e49c (EV Override-11.c lines 25547-25621).
// Name source: src/OpenEvo.MacOS ("DoSceneTransition(type, subtype)").
//
// Shows the mission briefing / news scene: loads the two backdrop PICTs (0x1b8e,
// 0x1b8f), opens modal dialog 0xbbb, and runs its modal loop until OK (item 1).
// If showMapButton and the "show on map" item (4) is hit, it pops the galaxy map
// (RunGalaxyMapDialog) around the player's current system (saved/restored so the map
// doesn't move the player) and refreshes the BBS / mission windows. Cleans up the
// PICTs and dialog on exit. Called after building news text in the mission flows
// (AcceptMission / ApplyMissionFailure / CheckMissionEncounter / …).
public static class DoSceneTransition
{
    // Port bridge for the modal-filter UPP (cell 0x10080fe4 → TVector 0x10082520 →
    // FUN_1003e6e4 = GenericAlertDialogFilter) — typed MacEvent shape.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = GenericAlertDialogFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return r;
    }

    public static void Run(byte showMapButton, byte refreshMissionWindow)
    {
        // The news/scene DialogPtr lives in the managed GameData.AlertDialog field.
        int[] backdropPicts = DialogScratch.ButtonPictPair;
        bool done = false;
        int modalFilterUpp = MacToolbox.NewRoutineDescriptor(GenericAlertDialogFilter.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(GenericAlertDialogFilter.FilterProc, FilterAdapter);

        // Backdrop PICTs 0x1b8e / 0x1b8f.
        for (int i = 0; i < backdropPicts.Length; i++)
            backdropPicts[i] = MacToolbox.GetPicture(i + 0x1b8e);

        GameData.AlertDialog = 0;
        GameData.AlertDialog = MacToolbox.GetNewDialog(0xbbb, 0, -1);
        if (GameData.AlertDialog != 0)
        {
            NewDialogHook.Run(GameData.AlertDialog, 0);
            RecenterWindowIntoPlayArea.Run(GameData.AlertDialog);
            MacToolbox.ShowWindow(GameData.AlertDialog);
            MacToolbox.SelectWindow(GameData.AlertDialog);
            MacToolbox.SetPort(GameData.AlertDialog);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            short[] itemHit = new short[11];
            do
            {
                MacToolbox.ModalDialog(modalFilterUpp, ref itemHit[0]);
                if (itemHit[0] == 1) done = true;
                if (itemHit[0] == 4 && showMapButton != 0)
                {
                    // Pop the galaxy map without moving the player: save + restore system.
                    short savedSystem = GameData.Player.NavTargetSpob;
                    RunGalaxyMapDialog.Run();
                    GameData.Player.NavTargetSpob = savedSystem;
                    if (SpaceportGlobals.DialogWindow != 0)
                    {
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                        RedrawSpaceportDialog.Run();
                    }
                    if (refreshMissionWindow != 0)
                    {
                        MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                    }
                    MacToolbox.SetPort(GameData.AlertDialog);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(GameData.AlertDialog));
                    RedrawGenericAlertDialog.Run();
                }
            } while (!done);

            for (int i = 0; i < backdropPicts.Length; i++)
            {
                if (backdropPicts[i] != 0)
                {
                    MacToolbox.HPurge(backdropPicts[i]);
                    MacToolbox.ReleaseResource(backdropPicts[i]);
                }
            }
            SetGamePortAndDevice.Run();
            MacToolbox.DisposeRoutineDescriptor(modalFilterUpp);
            MacToolbox.DisposeDialog(GameData.AlertDialog);
        }
    }
}
