using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 39480-39501.
//
// Speech-synth dispatch: if the TTS Manager was detected (DetectSpeechSupport),
// dispose any open speech channel, convert the C string in place to Pascal and
// SpeakString it, then optionally spin until SpeechBusy clears. The Mac speech
// engine is NOT ported — deliberate: MacToolbox.SpeakString is a documented
// no-op stub and SpeechBusy returns 0, so the wait loop exits immediately
// (and SpeechAvailable stays 0 anyway because Gestalt reports no TTS Manager).
public static class SpeakText
{
    /// Managed-string form: the raw int-textPtr overload (c2pstr'd a Str255 in EvoMemory)
    /// was caller-less and deleted with its EvoMemory converter — the lone caller (the
    /// About-EVÉ WarGames easter egg) already passes a C# literal, so c2pstr's in-place
    /// conversion is moot here.
    public static void Run(string text, short waitUntilDone)
    {
        if (SoundFilePlayState.SpeechAvailable != 0)
        {
            if (SoundFilePlayState.SpeechChannelHandle != 0)
            {   // Dead in the shipping binary: nothing ever sets SpeechChannelHandle
                // nonzero (see SoundFilePlayState), so this Dispose branch never runs.
                // Kept faithful.
                MacToolbox.DisposeSpeechChannel(SoundFilePlayState.SpeechChannelHandle);
                SoundFilePlayState.SpeechChannelHandle = 0;
            }
            MacToolbox.SpeakString(text);
            short speechBusy = waitUntilDone;
            while (speechBusy != 0)
            {
                speechBusy = (short)MacToolbox.SpeechBusy();
            }
        }
    }
}
