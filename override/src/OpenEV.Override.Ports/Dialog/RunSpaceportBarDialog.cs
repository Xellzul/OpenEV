using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10009eac (EV Override-11.c lines 5343-5479) — the spaceport BAR dialog
// (DLOG 0x3f5 = 1013), opened from the hub's item 0xb. Builds the news lines,
// regenerates the availability tables when landing on a new spob, fills the
// bar-person queue (priority flags&0x1000 persons first), then runs the modal:
//   item 1 leave   item 2 slot machine (needs 1000 cr)   item 3 news terminal
//   item 5 hire escort (gated EscortRoomAvailable; opens the shipyard in
//          escort mode)   item 6 bar-person mission encounter (fired by the
//          filter's random mash timer)
public static class RunSpaceportBarDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10080c18 -> FUN_1000a3ac) —
    // typed MacEvent shape (dialog 4-rules B8).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = BarDialogFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return r;
    }

    public static void Run()
    {
        bool done = false;
        short hitItem = default;

        int filterUpp = MacToolbox.NewRoutineDescriptor(SpaceportGlobals.BarFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(filterUpp, FilterAdapter);
        Text.BuildBarNewsText.Run();
        SpaceportGlobals.InBarFlag = 1;
        Graphics.Model.RenderGlobals.DrawGateFlag = 0;
        if ((SpaceportGlobals.BbsLastSpob != Core.Model.GameData.Ships[0].NavTargetSpob) ||
            (SpaceportGlobals.BbsLastSpob < 0))
        {
            for (short index = 0; index < Core.Model.GameData.RandomOdds.Length; index = (short)(index + 1))
            {
                Core.Model.GameData.RandomOdds[index] = (short)(Misc.SeedEvoRng.Run(100) + 1);
            }
            Mission.RefreshMissionAvailabilityTables.Run();
        }
        SpaceportGlobals.BbsLastSpob = Core.Model.GameData.Ships[0].NavTargetSpob;
        for (short index = 0; index < SpaceportGlobals.BarPersonQueue.Length; index = (short)(index + 1))
        {
            SpaceportGlobals.BarPersonQueue[index] = -1;
        }
        short queueCount = 0;
        for (short index = 0; index < Core.Model.MissionAvailGrid.Count; index = (short)(index + 1))
        {
            short persIdx = Core.Model.MissionAvailGrid.ByMode[1][index];
            if ((persIdx != -1) &&
               (((ushort)Mission.Model.MissionAvailTable.ReadShortAtByteOffset(persIdx * 0x12 + 0x10) & 0x1000) != 0))
            {
                SpaceportGlobals.BarPersonQueue[queueCount] = persIdx;
                queueCount = (short)(queueCount + 1);
            }
        }
        for (short index = 0; index < Core.Model.MissionAvailGrid.Count; index = (short)(index + 1))
        {
            short persIdx = Core.Model.MissionAvailGrid.ByMode[1][index];
            if ((persIdx != -1) &&
               (((ushort)Mission.Model.MissionAvailTable.ReadShortAtByteOffset(persIdx * 0x12 + 0x10) & 0x1000) == 0))
            {
                SpaceportGlobals.BarPersonQueue[queueCount] = persIdx;
                queueCount = (short)(queueCount + 1);
            }
        }
        if (queueCount < 1)
        {
            DialogScratch.SpaceportCommFaceIndex = -1;
        }
        else
        {
            DialogScratch.SpaceportCommFaceIndex = 0;
        }
        DialogScratch.SpaceportMashCounter = (short)(Misc.SeedEvoRng.Run(100) + 80);
        // strncpy-then-overwrite collapsed to one assignment (final value identical).
        DialogScratch.BarDescText = "";
        DialogScratch.BarDescText = Text.LoadDescriptionText.Load((short)(Core.Model.GameData.Ships[0].NavTargetSpob + 10000));
        DialogScratch.SpaceportPicts[0] = MacToolbox.GetPicture(0x1b60);
        DialogScratch.SpaceportPicts[1] = MacToolbox.GetPicture(0x1b61);
        for (short index = 2; index < DialogScratch.SpaceportPicts.Length; index = (short)(index + 1))
        {
            DialogScratch.SpaceportPicts[index] = MacToolbox.GetPicture(index + 0x1bb4);
        }
        DialogScratch.SpaceportDialogRecord = 0;
        DialogScratch.SpaceportDialogRecord = MacToolbox.GetNewDialog(0x3f5, 0, -1);   // behind = (WindowPtr)-1 = frontmost
        if (DialogScratch.SpaceportDialogRecord != 0)
        {
            NewDialogHook.Run(DialogScratch.SpaceportDialogRecord, 0);
            Graphics.RecenterWindowIntoPlayArea.Run(DialogScratch.SpaceportDialogRecord);
            MacToolbox.ShowWindow(DialogScratch.SpaceportDialogRecord);
            MacToolbox.SelectWindow(DialogScratch.SpaceportDialogRecord);
            MacToolbox.SetPort(DialogScratch.SpaceportDialogRecord);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            RedrawBarDialog.Run();
            do
            {
                MacToolbox.ModalDialog(filterUpp, ref hitItem);
                if (hitItem == 1)
                {
                    done = true;
                }
                if (hitItem == 2)
                {
                    DrawBarButtonRow.Run(-1);
                    if (Core.Model.GameData.Ships[0].Credits < 1000)
                    {
                        Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    }
                    else
                    {
                        RunSlotMachine.Run();
                    }
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(DialogScratch.SpaceportDialogRecord);
                    RedrawBarDialog.Run();
                }
                if (hitItem == 3)
                {
                    DrawBarButtonRow.Run(-1);
                    RunBarNewsDialog.Run();
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(DialogScratch.SpaceportDialogRecord);
                    RedrawBarDialog.Run();
                }
                if ((hitItem == 5) && Combat.EscortRoomAvailable.Run())
                {
                    DrawBarButtonRow.Run(-1);
                    Outfit.Model.ShipyardState.EscortMode = 1;
                    RunShipyardDialog.Run();
                    Outfit.RebuildOwnedOutfitsFromMarket.Run();
                    Graphics.RedrawHudStatusPanel.Run();
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(DialogScratch.SpaceportDialogRecord);
                    RedrawBarDialog.Run();
                }
                if (hitItem == 6)
                {
                    RunBarPersonEncounter.Run();
                }
            } while (!done);
            for (short index = 0; index < DialogScratch.SpaceportPicts.Length; index = (short)(index + 1))
            {
                if (DialogScratch.SpaceportPicts[index] != 0)
                {
                    MacToolbox.HPurge(DialogScratch.SpaceportPicts[index]);
                    MacToolbox.ReleaseResource(DialogScratch.SpaceportPicts[index]);
                }
            }
            MacToolbox.DisposeRoutineDescriptor(filterUpp);
            MacToolbox.DisposeDialog(DialogScratch.SpaceportDialogRecord);
            Graphics.RepaintGameWindow.Run();
        }
    }
}
