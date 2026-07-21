using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10046348 (EV Override-11.c lines 29259-29283). Distinct from
// Misc.CreditsScroller (FUN_10041ba0), which draws the actual scrolling
// credits/registration text — this one only marks the easter egg seen and
// plays a chime, blocking until playback finishes.
public static class PlayCreditsEasterEggChime
{
    public static void Run()
    {
        if (GameData.Player.CreditsEasterEggShown < 1)
        {
            GameData.Player.CreditsEasterEggShown = 1;
            GameData.Player.CreditsScrollSpeed = 50;   // written here only; no reader anywhere in the binary
            int sndHandle = LoadSndResource.Run(3000);
            SndPlay.Run(sndHandle, 42, 128, 128);
            if (sndHandle != 0)
            {
                while ((short)CountMatchingSoundVoices.Run(sndHandle) != 0)
                {
                    // Yield the game thread while the chime plays: a faithful
                    // no-yield spin here pegs a core for the chime's real
                    // duration and starves the host present thread.
                    System.Threading.Thread.Sleep(1);
                }
                MacToolbox.DisposePtr(sndHandle);
            }
        }
    }
}
