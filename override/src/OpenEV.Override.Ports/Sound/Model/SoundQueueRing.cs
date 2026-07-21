namespace OpenEV.Override.Ports.Sound.Model;

// Managed home for the 16-entry pending-sound ring the channel layer drains into
// PlaySoundOnChannel — was the BSS block at 0x10089e94 behind pointer cell
// 0x10081a50, with the scalar indices behind 0x10081a54/58/5c (BSS
// 0x10082440/42/44). The original entry is 10 bytes, UNALIGNED:
//   +0 int   SndHandle
//   +4 short Priority
//   +6 int   Param        (passed through to PlaySoundOnChannel's 4th arg)
//
// Readers: DrainSoundQueue (FUN_10076a04) consumes at (ReadIndex+1)&0xf;
// ResetSoundQueueIndices (FUN_100769dc) zeroes Count/WriteIndex/ReadIndex.
// Cell 0x10081a5c is the Count, 0x10081a54 the read index, 0x10081a58 the write index.
//
// ORIGINAL QUIRK: the binary contains NO writer for this ring.
// Nothing ever appends an entry, advances WriteIndex, or increments Count —
// the only accesses anywhere (cells 0x10081a50/54/58/5c, every toc/ppu alias
// checked) are the zeroing in ResetSoundQueueIndices and the consume in
// DrainSoundQueue. Like the completion list (see SoundProcs), the ring is never
// fed, so DrainSoundQueue's loop body is dead code. The pending-play scratch is
// the same story — see SoundChannels.
public static class SoundQueueRing
{
    public struct QueuedSound
    {
        public int SndHandle;
        public short Priority;
        public int Param;
    }

    public static readonly QueuedSound[] Entries = new QueuedSound[16];
    public static short Count;       // was *0x10082444 (cell 0x10081a5c)
    public static short ReadIndex;   // was *0x10082440 (cell 0x10081a54)
    public static short WriteIndex;  // was *0x10082442 (cell 0x10081a58)
}
