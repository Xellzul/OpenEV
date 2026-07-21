using System;

namespace OpenEV.Platform.Toolbox;

// Real implementations of Mac utility traps whose result is consumed by the
// ported transcriptions (so the `=> default` / no-op stubs produced wrong
// data). Strongly-typed overloads bind ahead of the params-object catch-all.
public static partial class MacToolbox
{
    /// Mac Get1Resource(type, id): like GetResource but searches only the
    /// current (top) resource file. The game keeps a single flat resource set, so it
    /// forwards to GetResource — which returns the materialised Handle, or 0 if
    /// absent (same as the old stub when the resource isn't present, so no
    /// regression; a real Handle when it is). InitRenderWindow fetches custom
    /// 'PMBl'/'PRBl' records this way.
    public static int Get1Resource(int type, int id) => GetResource(unchecked((uint)type), id);
    public static int Get1Resource(uint type, int id) => GetResource(type, id);
    public static int Get1Resource(MacResType type, int id) => GetResource((uint)type, id);

    // (StringToNum(int strPtr, int numPtr) — the Mac-memory Pascal-string parser that
    // wrote the result back through a pointer — was caller-less; the lone caller
    // (ResetWorldStateForNewPilot) uses the managed string form below. Deleted.)

    /// Managed StringToNum: same parse (leading '-' negates, digits accumulate
    /// base-10, non-digits ignored) on a C# string, returning the value.
    public static int StringToNum(string s)
    {
        long value = 0;
        bool negative = false;
        bool sawSign = false;
        foreach (char c in s)
        {
            if (c == '-' && !sawSign && value == 0) { negative = true; sawSign = true; continue; }
            if (c >= '0' && c <= '9')
            {
                value = value * 10 + (c - '0');
                sawSign = true;
            }
        }
        if (negative) value = -value;
        return (int)value;
    }

    /// Mac LMGetTicks(): the low-memory Ticks global — 60ths of a second since
    /// boot, same source as TickCount(). The no-arg overload binds ahead of the
    /// params catch-all (which returned 0).
    public static int LMGetTicks() => unchecked((int)Components.TickCount.Get());

    // (Munger(handle, offset, ptr1, len1, ptr2, len2) — the search-and-replace form
    // that drove Text/ReplaceAll — was caller-less after ReplaceAll was deleted (the
    // dësc <DST>-tag substitution became managed string.Replace in the main-game pass).
    // Deleted, taking its ~8 EvoMemory handle-walk reads with it.)

    /// Mac LMGetTime(): the low-memory Time global — seconds since the Mac epoch
    /// (1904-01-01). SeedEvoRng combines this with LMGetTicks() as the RNG seed;
    /// the old `=> 0` stubs left the seed a constant 0, so every launch replayed
    /// the identical "random" sequence. Real time restores per-run variability
    /// exactly as the Mac did. (Mac Time is unsigned 32-bit and wraps in 2040 —
    /// we mirror that wrap, which is fine for a seed.)
    public static int LMGetTime()
        => unchecked((int)(uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2082844800L));
}
