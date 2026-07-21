using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10054db0 (EV Override-11.c lines 34748-34799). Decompile lines 34782/34788 use
// the signed int→double magic idiom (`float cast == (double)(int)x`), collapsed here to a
// plain cast. The benchmark's tuning consts live in the PEF data segment, read through
// PowerPC TOC offsets in the decompile; they're inlined below as plain literals.
public static class RunCpuSpeedBenchmark
{
    public static void Run()
    {
        int[] sampleCounts = new int[10];

        ShowGenericAlert.Run();
        uint totalIterations = 0;
        for (int sampleIndex = 0; sampleIndex < 10; sampleIndex++)
        {
            sampleCounts[sampleIndex] = 0;
            int startTick = (int)MacToolbox.TickCount();
            while (true)
            {
                uint currentTick = MacToolbox.TickCount();
                // Cast to uint before adding: C# promotes a bare `int + uint` to long (no 32-bit
                // wraparound), unlike the ASM's cmplw/decompile's `int + unsigned int` (both 32-bit
                // unsigned). Bit-identical for realistic tick counts either way, but this matches.
                if ((uint)startTick + 2U < currentTick) break;
                sampleCounts[sampleIndex] = sampleCounts[sampleIndex] + 1;
            }
            totalIterations = totalIterations + (uint)sampleCounts[sampleIndex];
            // ppuVar2 = local_7c dropped: local_7c uninitialized in decompile (decompiler artifact)
        }
        // i2d bias @0x10082150, divisor 10.0 @0x10082130, 7200f @0x10082128.
        uint speedScaled = (uint)((double)(int)totalIterations / 10.0);
        // local_28 = (longlong)(int)uStack_2c — dead store in the decompile (never read); dropped.
        // Decompile L34788: *pdVar1 = (double)((float)ref / (float)(iters - bias)) is a plain
        // double-widen of the float quotient — not a PpcMagic bit-reinterpret; keep the outer cast.
        double speed = (double)(7200f / (float)(int)speedScaled);
        if (speed <= 3.0 /* @0x10082120 */)
        {
            if (speed < 1.05 /* @0x10082118 */)
            {
                speed = 1.0 /* @0x10082110 */;
            }
        }
        else
        {
            speed = 3.0 /* @0x10082120 */;
        }
        WorldState.CpuSpeedScale = speed;
        DisposeCurrentAlertDialog.Run();
        return;
    }
}
