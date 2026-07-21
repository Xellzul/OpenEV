// Port of FUN_100413ac (EV Override-11.c lines 26810-26839).

namespace OpenEV.Override.Ports.Misc;

// Tech-level discount + round-down quantizer for shop prices.
//
// PPC ABI note: every call site passes a LEADING double (a price-scale float read
// through the OutfitLayoutScaleTableSlot pointer) in f1 — but the decompiled body
// declares only 4 GPR params and NEVER reads f1 (a decompile-dropped FP arg that the
// original function genuinely ignores). The 5-arg overload therefore drops arg 1
// and forwards the GPR args shifted by one.
public static class PriceQuantize
{
    public static int Run(int unusedFpScale, int price, int spobRecByteOffset, short outfitTechLevel, int spobTechLevel)
        => Run(price, spobRecByteOffset, outfitTechLevel, (short)spobTechLevel);

    public static int Run(int price, int spobRecByteOffset, short outfitTechLevel, short spobTechLevel)
    {
        // spobRecByteOffset (navTargetSpob * 0x48) is passed by every caller but is
        // unused by the original body — kept for the faithful call shape.
        _ = spobRecByteOffset;

        // High-tech discount: 3% per tech level the spob exceeds the outfit, for
        // outfit/spob tech 1..5 and prices above 99. The 0.01 scale is the data-seg
        // double at 0x10081fe0 (dumped); the decompile's `(float-cast) - _DAT_10082058`
        // pairs are complete SIGNED int->double idioms = plain (double) casts.
        // Grouped as price * (0.01 * factor), NOT (price * 0.01) * factor: the ASM computes
        // the 0.01*factor sub-product first (fmul f0,f2,f0) and multiplies by price second
        // (fmul f0,f3,f0) — the decompile's flat left-to-right `*` chain doesn't preserve
        // that order, and FP multiply isn't associative.
        if (outfitTechLevel < 6 && spobTechLevel < 6 && outfitTechLevel < spobTechLevel && 99 < price)
        {
            price = (int)(uint)((double)price *
                                (0.01 * (double)((spobTechLevel - outfitTechLevel) * -3 + 100)));
        }
        // Round down to 10 / 100 / 1000 steps by magnitude.
        if (price < 100001)
        {
            if (price < 10001)
            {
                if (100 < price)
                {
                    price = (price / 10) * 10;
                }
            }
            else
            {
                price = (price / 100) * 100;
            }
        }
        else
        {
            price = (price / 1000) * 1000;
        }
        return price;
    }
}
