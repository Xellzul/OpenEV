using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 50106-50135.
// Walks the 16-entry pending-sound ring: for each queued entry (consumed at the
// masked post-increment of ReadIndex) find a free/evictable channel for its
// priority and play it; stop when the ring empties, no channel is available, or
// DrainEnabled drops. DEAD IN PRACTICE: the binary never feeds the ring (Count
// only ever decrements from 0 writes — see SoundQueueRing header), so the loop
// body never runs.
public static class DrainSoundQueue
{
    public static void Run()
    {
        short queueCount = SoundQueueRing.Count;
        while (queueCount != 0)
        {
            // The probed priority lives at entry (ReadIndex + 1) — the same entry
            // the masked post-increment below goes on to play.
            // ORIGINAL QUIRK: the probe index is NOT masked, so with ReadIndex ==
            // 15 the Mac read a garbage priority 10 bytes PAST the 160-byte ring
            // block. A managed array can't read past; & 0xf reads entry 0 — which
            // IS the entry played after the wrap (dead path anyway, ring never fed).
            short probeIndex = (short)(SoundQueueRing.ReadIndex + 1);
            int channel = FindEvictableSoundChannel.Run(SoundQueueRing.Entries[probeIndex & 0xf].Priority);
            if ((short)channel == 0)
            {
                return;
            }
            SoundQueueRing.Count = (short)(SoundQueueRing.Count - 1);
            SoundQueueRing.ReadIndex = (short)MacToolbox.BitAnd((short)(SoundQueueRing.ReadIndex + 1), 0xf);
            SoundQueueRing.QueuedSound entry = SoundQueueRing.Entries[SoundQueueRing.ReadIndex];
            PlaySoundOnChannel.Run(entry.SndHandle, entry.Priority, channel, entry.Param);
            if (!SoundChannels.DrainEnabled)
            {
                return;
            }
            queueCount = SoundQueueRing.Count;
        }
        return;
    }
}
