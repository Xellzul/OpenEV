namespace OpenEV.Override.Ports.Sound.Model;

// Managed home for the GAME-CHANNEL layer of the sound subsystem — 16 Mac
// SndChannel slots plus the pending-play scratch, kept by the original behind the
// pointer-cell band 0x10081a38..0x10081a88. TOC aliases GameToc-0x6bXX/-0x6cXX and
// the decompile's ppuVar[-0x1aXX] (pointer-indexing = byte offset x4) reach the same cells.
//
// Cell map (cell -> BSS target -> field here), verified against the decompile:
//   0x10081a84 -> 0x10089fb4  Channels[i].Handle ('Schn' sentinel in the port)
//   0x10081a80 -> 0x10089fa4  Channels[i].Busy (byte; ppu -0x1af8)
//   0x10081a68 -> 0x10089f44  Channels[i].ExpiryTick
//   0x10081a64 -> 0x10089f84  Channels[i].PlayingPriority (ppu -0x1aff)
//   0x10081a60 -> 0x10089f34  Channels[i].Reserved (byte)
//   0x10081a88 -> 0x1008245c  ChannelCount (short; InitSoundSubsystem sets 1; toc-0x6bd8)
//   0x10081a70 -> 0x10082452  HardwareStereoCapable (byte; Gestalt 'snd ' bit0; ppu -0x1afc)
//   0x10081a6c -> 0x10082454  DrainEnabled (byte; ppu -0x1afd)
//   0x10081a4c -> 0x10082455  QueueResetFlag (byte)
//   0x10081a7c -> 0x10082453  UseChannelStatusFlag (byte)
//   0x10081a78 -> 0x10082458  NewChannelInitFlags (InitSoundSubsystem defaults 0x84 if 0)
//   0x10081a74 -> TVector FUN_1007700c — the SndNewChannel user routine (bufferCmd
//                 0x51 + callBackCmd 0xd dispatcher). The port: channel playback is
//                 host-bridged, no field needed.
//   0x10081a44 -> 0x1008244e  PendingSndHandle (toc-0x6c1c; ppu -0x1b07)
//   0x10081a3c -> 0x1008244a  PendingPriority (short)
//   0x10081a40 -> 0x1008244c  RetryPendingWhenNoChannel (byte; toc-0x6c20)
//   0x10081a38 -> 0x10082446  PendingImmediateParam (toc-0x6c28)
//   0x10081a48 -> 0x10082456  FlagA48 (byte; cleared at init; toc-0x6c18)
public static class SoundChannels
{
    // The Sound Manager channel count the original hardcodes throughout (index loops,
    // the evict-oldest scan, the drain consumer). Shared so callers outside this file
    // don't re-hardcode the bound.
    public const int ChannelSlotCount = 16;

    public sealed class ChannelState
    {
        public int Handle;            // 0 = not allocated
        public byte Busy;             // 1 while a sound is playing on the channel
        public int ExpiryTick;        // TickCount deadline computed by PlaySoundOnChannel
        public short PlayingPriority; // priority of the sound currently on the channel
        public byte Reserved;         // per-channel reserved flag (cleared at init)
    }

    public static readonly ChannelState[] Channels = NewChannels();

    private static ChannelState[] NewChannels()
    {
        var c = new ChannelState[ChannelSlotCount];
        for (int i = 0; i < c.Length; i++) c[i] = new ChannelState();
        return c;
    }

    public static short ChannelCount;
    public static bool HardwareStereoCapable;
    public static bool DrainEnabled;
    public static byte QueueResetFlag;
    public static byte UseChannelStatusFlag;
    public static int NewChannelInitFlags;

    // Pending single-sound retry scratch (a play that found no free channel).
    // ORIGINAL QUIRK (verified): the binary never SETS any of these — every
    // access to cells 0x10081a38/3c/40/44 (all toc/ppu aliases checked) is a
    // zeroing (init/teardown/tick) or a read; like the queue ring and the
    // completion list, TickSoundSubsystem's retry branch can never fire.
    public static int PendingSndHandle;
    public static short PendingPriority;
    public static byte RetryPendingWhenNoChannel;
    public static int PendingImmediateParam;
    public static byte FlagA48;
}
