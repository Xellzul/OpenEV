using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

public static class DetectSpeechSupport
{
    // FUN_1005ec58, EV Override-11.c lines 39441-39474: probe Gestalt for the
    // TTS Manager and set the speech-available byte if it reports >0 voices.
    public static void Run()
    {
        // The decompile writes both 0 (here) and 1 (below) through the SAME relocated
        // ptr cell (0x10081204); SoundFilePlayState.SpeechAvailable is that cell's target byte.
        SoundFilePlayState.SpeechAvailable = 0;

        // NO-OP: MacToolbox.Gestalt(int,out int) is stubbed to noErr with dataOut=0 (MacToolbox.cs)
        // — no selector is actually dispatched. gestaltResponse always reads 0 here, so the bit-31
        // 'ttsc' test below always fails and SpeechAvailable can never end up set to 1 in the port.
        // Gestalt output slot (undefined1[8] in decompile; only low 4 bytes used as int — scalar, not an address).
        short gestaltErr = MacToolbox.Gestalt(0x76657273 /*'vers'*/, out int gestaltResponse);
        if (gestaltErr == 0)
        {
            gestaltErr = MacToolbox.Gestalt(0x74747363 /*'ttsc'*/, out gestaltResponse);
            bool ttsManagerPresent = false;
            if (gestaltErr == 0)
            {
                if (MacToolbox.BitTst(gestaltResponse, 0x1f) != 0)
                { // bit 31
                    ttsManagerPresent = true;
                }
            }
            byte bitSet = 0;
            if (ttsManagerPresent)
            {
                bitSet = (byte)MacToolbox.BitTst(gestaltResponse, 0x1e); // bit 30
            }
            if (bitSet != 0)
            {
                // NO-OP: MacToolbox.CountVoices is an unwired params-absorber stub (UnwiredStubs.cs)
                // — it never writes voiceCount, so this branch is currently unreachable-in-effect too.
                short[] voiceCount = new short[2];
                MacToolbox.CountVoices(voiceCount);
                if (0 < voiceCount[0])
                {
                    SoundFilePlayState.SpeechAvailable = 1;
                }
            }
        }
        return;
    }
}
