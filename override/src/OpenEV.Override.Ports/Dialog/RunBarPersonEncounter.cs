using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Mission;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000bca8 (EV Override-11.c lines 6133-6168) — a bar person approaches the
// player ("DeleteSelectedSwatch" was an early transcription misname). Fired by the bar
// filter's random mash timer (item 6): runs the current queue-front person's
// mission-offer dialog, removes that person from the queue (compacting it),
// redraws hub + bar, advances the comm-face index and rearms the timer.
public static class RunBarPersonEncounter
{
    public static void Run()
    {
        int queueLength = SpaceportGlobals.BarPersonQueue.Length;
        short faceIndex = DialogScratch.SpaceportCommFaceIndex;
        if (-1 < faceIndex && faceIndex < queueLength &&
            -1 < SpaceportGlobals.BarPersonQueue[faceIndex])
        {
            RunSingleMissionDialog.Run(SpaceportGlobals.BarPersonQueue[faceIndex]);
            short departed = SpaceportGlobals.BarPersonQueue[faceIndex];
            var compacted = new short[queueLength];
            for (short i = 0; i < queueLength; i = (short)(i + 1))
            {
                compacted[i] = -1;
            }
            short writeIndex = 0;
            for (short i = 0; i < queueLength; i = (short)(i + 1))
            {
                if (departed != SpaceportGlobals.BarPersonQueue[i] &&
                    SpaceportGlobals.BarPersonQueue[i] != -1)
                {
                    compacted[writeIndex] = SpaceportGlobals.BarPersonQueue[i];
                    writeIndex = (short)(writeIndex + 1);
                }
            }
            for (short i = 0; i < queueLength; i = (short)(i + 1))
            {
                SpaceportGlobals.BarPersonQueue[i] = compacted[i];
            }
            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);            // the HUB window (*PTR_DAT_10080ba0)
            RedrawSpaceportDialog.Run();
            MacToolbox.SetPort(DialogScratch.SpaceportDialogRecord);     // the bar window (*(toc-0x28cc))
            RedrawBarDialog.Run();
            DialogScratch.SpaceportCommFaceIndex = (short)(DialogScratch.SpaceportCommFaceIndex + 1);
            DialogScratch.SpaceportMashCounter = 0;
        }
    }
}
