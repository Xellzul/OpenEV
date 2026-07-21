using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10060928 (EV Override-11.c lines 40398-40409).
//
// Called from TickShipAI with the ship class's sprite scale: stores
// FixRatio((int)(1000.0 * scale), 1000) — the scale as a Fixed ratio.
public static class ComputeScrollRatio
{
    // Managed home for the stored ratio. The decompile writes it through an
    // unrecovered base register (`local_2c + 0x20a8`, the usual lost-RTOC
    // artifact); under the in-game GameToc that resolves to 0x1008a708 — which
    // in the ORIGINAL PEF layout is byte +8 of the boarding-chime
    // SoundPlayRequest record 0x1008a700..1b (now the managed
    // SoundMixer.BoardingChimeRequest). No reader of the
    // cell exists in the binary (write-only), so the managed field does not
    // replicate the original record clobber.
    public static int ScrollRatioFixed;

    public static void Run(double scrollFraction)
    {
        ScrollRatioFixed = MacToolbox.FixRatio((int)(1000.0 * scrollFraction), 1000);
    }
}
