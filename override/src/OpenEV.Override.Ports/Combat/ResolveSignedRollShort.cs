using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Combat;

// FUN_10048b00 (EV Override-11.c 30346-30369) — resolve a "negative means random" short: a value
// >= -1 passes through unchanged; a value < -1 means "random in [|v|, 2|v|)", returning |v| + rng(|v|).
// Value form, not the decompile's in-place short mutation — the sole caller (LoadBarPersonResources)
// now holds a managed record, so the rolled value is returned instead of written back through a pointer.
public static class ResolveSignedRollShort
{
    public static short Run(short value)
    {
        if (value < -1)
        {
            int magnitude = -value;
            short roll = (short)SeedEvoRng.Run((short)magnitude);
            return (short)(magnitude + roll);
        }
        return value;
    }
}
