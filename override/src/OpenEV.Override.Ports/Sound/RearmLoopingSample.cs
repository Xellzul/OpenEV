using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 50326-50344.
// Renamed from PlaySampleAtFreq (Rule 12): the body never computes or sets a
// frequency anywhere — it only re-queues a bufferCmd and re-arms a callBackCmd.
//
// The CHANNEL layer's SndNewChannel userRoutine — the TVector behind
// cell 0x10081a74, see the SoundChannels header. It is a Mac
// SndCallBackProc `(SndChannelPtr chan, SndCommand *cmd)`: handed the
// callBackCmd's record {+0 cmd, +2 param1, +4 param2 = SoundHeader ptr} it
// re-queues the sample — bufferCmd {81, 0, headerPtr} then callBackCmd
// {13, param1, headerPtr} — so the channel replays the buffer and re-arms
// ITSELF: a looping sample player. The port never installs the userRoutine (no real
// Sound Manager channel exists behind PlaySoundOnChannel's 'Schn' sentinel;
// see that class's header), so this stays an uncalled faithful leaf; the
// SndCommand record arrives decomposed.
public static class RearmLoopingSample
{
    public static void Run(int soundChannel, short cmdParam1, int soundHeaderPtr)
    {
        if (soundHeaderPtr != 0)
        {
            MacToolbox.SndDoCommand(soundChannel, 81, 0, soundHeaderPtr);                 // bufferCmd
            MacToolbox.SndDoCommand(soundChannel, 13, (ushort)cmdParam1, soundHeaderPtr);  // callBackCmd
        }
    }
}
