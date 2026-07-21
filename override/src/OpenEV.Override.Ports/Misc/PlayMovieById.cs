using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Mission;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_100602d8 (EV Override-11.c lines 40213-40272): scans the movie table (MovieTable,
// 128 records) for the record whose id matches movieId. When found it refreshes the
// windows that display behind the movie (mission dialog / BBS / bar) and, in trigger
// mode, plays the QuickTime movie (PlayQuickTimeMovie on the record's Name). Returns 1
// UNLESS the movie is a one-shot that has ALREADY been played (Flags bit 0x2) — i.e.
// 1 = "not shown as a consumed one-shot; caller should present its own fallback". The
// first-entry intro uses this: when no intro movie is present/playable the gate returns
// 1, so ShowIntroCutsceneAndStartMusic shows the static PICT 8200 + scrolling text
// instead.
//
// trigger (param_2): nonzero = explicit play-now request; zero = passive refresh.
public static class PlayMovieById
{
    public static int Run(short movieId, byte trigger)
    {
        int result = 1;
        foreach (var movie in GameData.Movies)
        {
            if (movieId != movie.MovieId) continue;

            // Refresh the windows showing this movie (passive mode, or a replayed one-shot).
            if (trigger == 0 || (movie.Flags & 2) != 0)
            {
                if (MissionBoardGlobals.DialogWindow != 0)
                {
                    MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                    bool notInSpaceport = RenderGlobals.DrawGateFlag == 0;
                    if (notInSpaceport && DialogScratch.SpaceportDialogRecord == 0)
                        RedrawMissionBbsDialog.Run();      // FUN_1004d31c
                    else
                        RedrawSingleMissionDialog.Run();   // FUN_100515d8
                }
                if (SpaceportGlobals.DialogWindow != 0)
                {
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();           // FUN_10037bb4
                }
                if (DialogScratch.SpaceportDialogRecord != 0)
                {
                    MacToolbox.SetPort(DialogScratch.SpaceportDialogRecord);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportDialogRecord));
                    RedrawBarDialog.Run();                 // FUN_1000a560
                }
            }

            // Play the movie + consume the one-shot flag.
            if (trigger != 0)
            {
                if ((movie.Flags & 2) != 0 || (movie.Flags & 1) == 0)
                {
                    PlayQuickTimeMovie.Run(movie.Name, 0);   // FUN_10060504
                    if ((movie.Flags & 2) != 0) result = 0;
                }
            }
            // Decompile comma-operator: when passive + auto-play, ALWAYS play, then test.
            else if ((movie.Flags & 1) != 0)
            {
                PlayQuickTimeMovie.Run(movie.Name, 0);       // FUN_10060504
                if ((movie.Flags & 2) != 0) result = 0;
            }
        }
        return result;
    }
}
