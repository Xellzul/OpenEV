using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49408-49449.
//
// Walk a 'snd ' resource's command list for a bufferCmd/soundCmd carrying the
// dataOffset flag and return that command's param2 — the byte offset of the
// SoundHeader inside the resource. 0 when the format or commands don't match.
// Sole caller in the binary: FUN_10075450 (LoadSndResource), whose port form is
// SndResourceRegistry.LoadAndRegister — it passes the GetResource bytes here.
// B3 rewrote the walk onto the managed byte[] (the original double-dereffed
// the Mac Handle; the initial transcription read it through the EvoMemory arena where no
// resource bytes live — always 0).
public static class FindSoundCommandData
{
    // SndCommand type codes carrying the dataOffsetFlag (0x8000): bufferCmd
    // (0x51) and soundCmd (0x50) OR'd with it, as 16-bit signed values.
    private const short BufferCmdFlagged = -0x7faf; // 0x8051
    private const short SoundCmdFlagged = -0x7fb0;  // 0x8050

    public static int Run(byte[] sndResource)
    {
        bool done = false;
        int dataOffset = 0;
        int cmdPtr = 0;
        short format = BigEndian.ReadInt16OrZero(sndResource, 0);                // sVar4 = *psVar3
        if (format == 2)
        {
            cmdPtr = 4;                                          // format-2: skip {format, refCount}
        }
        else if (format < 2 && 0 < format)
        {
            // format-1: skip the synth/modifier list — (numModifiers*3 + 2)
            // SHORTS (psVar3 + psVar3[1]*3 + 2 is short-pointer arithmetic).
            cmdPtr = (BigEndian.ReadInt16OrZero(sndResource, 2) * 3 + 2) * 2;
        }
        else
        {
            done = true;
        }
        // ORIGINAL (kept): on an unknown format the command count is read from
        // the UNADVANCED pointer — i.e. the format word itself — and the loop
        // is entered just to bail through the `done` break with 0.
        short cmdCount = BigEndian.ReadInt16OrZero(sndResource, cmdPtr);
        cmdPtr += 2;
        while (true)
        {
            if (cmdCount < 1)
            {
                return dataOffset;
            }
            if (done) break;
            short cmd = BigEndian.ReadInt16OrZero(sndResource, cmdPtr);
            if (cmd == BufferCmdFlagged || cmd == SoundCmdFlagged)
            {
                dataOffset = BigEndian.ReadInt32OrZero(sndResource, cmdPtr + 4);  // SndCommand.param2
                done = true;
            }
            else
            {
                cmdPtr += 8;                                     // SndCommand stride {cmd,param1,param2}
                cmdCount -= 1;
            }
        }
        return dataOffset;
    }
}
