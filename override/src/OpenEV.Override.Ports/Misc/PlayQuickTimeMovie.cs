using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_10060504 (EV Override-11.c lines 40274-40373): play the named QuickTime movie
// file, centred in a bordered window over the game screen, until it finishes or the
// player clicks.
//
// The Mac body is gated on `EnterMovies() == 0`; the trap reports available once the
// host installs MacToolbox.MovieFileResolver (silent playback — rpza/jpeg/SVQ1 video,
// no audio codecs). With no resolver EnterMovies returns -1 and the movie is skipped —
// exactly a Mac without QuickTime: the intro and every mission "movie" degrade to
// their dësc text + PICT fallback (PlayMovieById returns "show fallback"). The base
// game ships ZERO 'dëqt' descriptors and ZERO movie files — only plug-ins (E3 "The
// Frozen Heart") ever reach this with a real movie.
public static class PlayQuickTimeMovie
{
    // Movie files live in the EV Plug-Ins folder: the decompile resolves the folder
    // via GetCatalogStartDir(refNum at 0x100870d2, toc-0x3ae1) — the 12-byte Pascal
    // string "EV Plug-Ins" (unk_84B7F, same literal family as OpenPluginResourceFiles').
    private const string PluginsFolderName = "EV Plug-Ins";

    // movieFileName: the movie record's resource name (managed string) — the QuickTime
    // movie FILE name FSMakeFSSpec resolves in the plug-ins folder.
    // suppressWindowSetup (cStack0000001f): skip the NewDialogHook/recenter calls.
    public static void Run(string movieFileName, byte suppressWindowSetup)
    {
        if (GamePrefs.QuickTimeMoviesDisabled != 0)
            return;
        if (MacToolbox.EnterMovies() != 0)
            return;

        // Locate the plug-ins folder (FUN_10061a28) — its stub chain always succeeds
        // with dir 0; the real file lookup happens inside OpenMovieFile's resolver.
        short err = (short)Resource.GetCatalogStartDir.Run(
            Resource.PluginResourceRefs.Ref(1), PluginsFolderName, out int startDir, out short startVRef);
        if (err != 0) { MacToolbox.ExitMovies(); return; }

        MacToolbox.FSMakeFSSpec(startVRef, startDir, movieFileName);
        err = MacToolbox.OpenMovieFile(movieFileName, out short movieResRef);
        if (err != 0) { MacToolbox.ExitMovies(); return; }

        MacToolbox.NewMovieFromFile(out int movie, movieResRef, movieFileName);
        MacToolbox.CloseMovieFile(movieResRef);

        // Centre the movie box over the game window's port rect (+0xc..+0x12), with
        // the decompile's round-toward-zero halving on both spans.
        short[] box = new short[4];
        MacToolbox.GetMovieBox(movie, box);
        MacToolbox.OffsetRect(box, (short)-box[1], (short)-box[0]);
        int boxW = box[3] - box[1], portW = GlobalState.PortRight - GlobalState.PortLeft;
        int boxH = box[2] - box[0], portH = GlobalState.PortBottom - GlobalState.PortTop;
        MacToolbox.OffsetRect(box, (short)(portW / 2 - boxW / 2), (short)(portH / 2 - boxH / 2));
        MacToolbox.SetMovieBox(movie, box);

        MacToolbox.InsetRect(box, -1, -1);
        int wnd = MacToolbox.NewMovieWindow(box);   // NewCWindow(0, &box, toc-0x644c, 0, plainDBox, 0, 1, -1)
        MacToolbox.InsetRect(box, 1, 1);

        if (suppressWindowSetup == 0)
        {
            NewDialogHook.Run(wnd, 0);                 // FUN_100583c4
            RecenterWindowIntoPlayArea.Run(wnd);       // FUN_100583c8
        }
        MacToolbox.ShowWindow(wnd);
        MacToolbox.SelectWindow(wnd);
        MacToolbox.SetPort(wnd);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(box);
        MacToolbox.ForeColor(QuickDrawColor.White);
        MacToolbox.FrameRect(box);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.SetMovieGWorld(movie, wnd, 0);

        // Rebase the box to window-local coords, 1px inside the frame.
        MacToolbox.OffsetRect(box, (short)-box[1], (short)-box[0]);
        MacToolbox.OffsetRect(box, 1, 1);
        MacToolbox.SetMovieBox(movie, box);

        MacToolbox.GoToBeginningOfMovie(movie);
        MacToolbox.SetMovieRate(movie, 0x10000);
        MacToolbox.StartMovie(movie);
        while (!MacToolbox.IsMovieDone(movie))
        {
            MacToolbox.MoviesTask(movie, 0);
            if (MacToolbox.Button())
            {
                MacToolbox.FlushEvents((EventMask)3, 0);   // decompile literal (mDown | everyEvent bit 0)
                break;
            }
        }
        MacToolbox.CloseWindow(wnd);
        MacToolbox.DisposeMovie(movie);
        MacToolbox.ExitMovies();
        SetGamePortAndDevice.Run();                    // FUN_1007ab1c
        MacToolbox.ForeColor(QuickDrawColor.Black);
    }
}
