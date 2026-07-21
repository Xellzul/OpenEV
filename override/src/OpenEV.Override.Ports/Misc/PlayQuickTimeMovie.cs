using OpenEV.Override.Ports.Core.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_10060504 (EV Override-11.c lines 40274-40373): play the named QuickTime movie
// file, centred in a bordered window over the game screen, until it finishes or the
// player clicks.
//
// NO-OP: the whole Mac body is gated on `EnterMovies() == 0`. This is a cross-platform
// C# port with no QuickTime runtime, so EnterMovies returns -1 and the movie is skipped
// — the exact behaviour of running EVO on a Mac without QuickTime installed: the intro
// and every mission "movie" degrade to their dësc text + PICT fallback (PlayMovieById
// returns "show fallback"). This is NOT a missing feature for the base game: EV
// Override 1.0.2 ships ZERO 'dëqt' movie descriptors and ZERO .mov files (verified
// across all six data forks + the application fork + the plug-ins + the Register app),
// so PlayMovieById never matches a movieId and this function is never reached with a
// real movie. The QT path only ever mattered to a plug-in that bundled its own .mov.
public static class PlayQuickTimeMovie
{
    // movieFileName: the movie record's resource name (managed string) — the QuickTime
    // movie FILE name FSMakeFSSpec would resolve in the game directory.
    // suppressWindowSetup (cStack0000001f): skip the NewDialogHook/recenter calls.
    public static void Run(string movieFileName, byte suppressWindowSetup)
    {
        if (GamePrefs.QuickTimeMoviesDisabled != 0)
            return;

        // sVar7 = EnterMovies(): the port's stub returns -1 (no QuickTime) → skip the body, exactly
        // as the Mac does when QuickTime is unavailable. ExitMovies is NOT called on this
        // early failure (matches the decompile: the EnterMovies != 0 branch just returns).
        if (MacToolbox.EnterMovies() != 0)
            return;

        // Body deferred (unreachable while EnterMovies is the -1 stub); re-derive from the
        // decompile/ASM above if a real QT runtime is ever wired: resolve the game folder →
        // FSMakeFSSpec → OpenMovieFile → NewMovieFromFile → GetMovieBox, centre on the screen
        // portRect → SetMovieBox → NewCWindow (+NewDialogHook/recenter unless
        // suppressWindowSetup) → StartMovie → pump MoviesTask until done/clicked → CloseWindow,
        // DisposeMovie, ExitMovies. Every Mac error exit calls ExitMovies and returns — a
        // graceful skip, never a crash. Mirror that:
        _ = movieFileName;
        _ = suppressWindowSetup;
        MacToolbox.ExitMovies();
    }
}
