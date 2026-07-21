using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_1007520c (EV Override-11.c lines 48941-48989).
// Removes every mixer voice matching the query — voice id, snd handle, or 0 =
// flush all — compacting the voice array down and firing each removed voice's
// completion with kind 2 (flushed).
public static class FlushMixQueueEntries
{
    public static void Run(int flushQuery)
    {
        // The decompile gates the ENTIRE body behind `DAT_10082430 != '\0'` (sound
        // booted) — including the host-bridge stop call below, which must stay
        // INSIDE this gate so an un-booted subsystem remains a true no-op.
        if (EvoGlobals.IsSoundSubsystemBooted)
        {
            // The one stop-side host-bridge call (see MacToolbox.StopSndForHandle for
            // the 0-vs-handle semantics). E.g. this cuts the title row-reveal snd 601
            // at inner iteration 14.
            MacToolbox.StopSndForHandle(flushQuery);

            VoiceState[] voices = SoundMixer.Voices;
            short index = 0;
            while (index < SoundMixer.ActiveVoiceCount)
            {
                if (flushQuery == voices[index].Id ||
                    voices[index].SoundHandle == flushQuery ||
                    flushQuery == 0)
                {
                    SoundCompletionProc? completion = voices[index].CompletionProc;
                    int interruptMask = SoundProcs.SaveInterruptMask();
                    SoundMixer.ActiveVoiceCount--;
                    int newCount = SoundMixer.ActiveVoiceCount;
                    // BlockMoveData compaction DOWN: records index+1..newCount
                    // shift to index..newCount-1, then the 13-field zero list runs
                    // on the freed tail slot (VoiceState.Clear).
                    for (int i = index; i < newCount; i++)
                        voices[i].CopyFrom(voices[i + 1]);
                    voices[newCount].Clear();
                    SoundProcs.RestoreInterruptMask(interruptMask);
                    if (completion != null)
                    {
                        // ORIGINAL (kept): arg 2 is the FLUSH QUERY param, not the
                        // removed voice's id, and the scratch record is passed
                        // through UNFILLED (stale from the last doubleback).
                        completion(2, flushQuery, SoundMixer.CallbackScratch);
                    }
                }
                else
                {
                    index++;
                }
            }
        }
    }
}
