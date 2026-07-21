namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 50383-50423.
//
// NOTE (B2b): the historical "UI SFX config" name is a MISNAME. The 0x21-short
// block this fills (BSS 0x10089ff8 behind cell 0x10081a90, toc-0x6bd0 / ppu
// -0x1af4) is a synthesized DITL (dialog item list) for RunMultiButtonModalDialog
// (FUN_10077460) — its ONLY reader. Decoded:
//   [0]        itemCount-1 = 3 (the caller overwrites it with buttonCount)
//   [1..2]     item 1 placeholder handle
//   [3..6]     item 1 Rect (10,27,90,225) — the message statText
//   [7]        0x8808 = itemType 0x88 (statText|disabled) + dataLength 8
//   [8..0xb]   "^0^1^2^3" — the ParamText substitution placeholders
//   [0xc..0x12]  item 2: Rect (104,140,124,210), type 0x04 (button), len 0
//   [0x13..0x19] item 3: Rect (104,30,124,100),  type 0x04 (button), len 0
//   [0x1a..0x20] item 4: Rect (72,30,92,100),    type 0x04 (button), len 0
// The caller BlockMoves it into a fresh handle = the decompile-dropped 9th
// NewDialog argument (the items list), and patches [5] (statText bottom) /
// [0] (item count) per call. Managed home below.
public static class InitUiSfxConfig
{
    // Was BSS 0x10089ff8 (cell 0x10081a90) — the synthesized multi-button DITL.
    public static readonly short[] MultiButtonDitlTemplate = new short[0x21];

    public static void Run()
    {
        short[] ditl = MultiButtonDitlTemplate;
        ditl[0] = 3;          // itemCount-1 (overwritten with buttonCount by the caller)
        ditl[1] = 0;          // item 1 placeholder handle
        ditl[2] = 0;
        ditl[3] = 10;         // item 1 Rect.top    — message statText
        ditl[4] = 27;         //        Rect.left
        ditl[5] = 90;         //        Rect.bottom (caller shrinks to [0x1c]-3 when 3 buttons)
        ditl[6] = 225;        //        Rect.right
        ditl[7] = -30712;     // type 0x88 statText|disabled, dataLength 8 (0x8808)
        ditl[8] = 0x5e30;     // "^0"  — ParamText placeholders
        ditl[9] = 0x5e31;     // "^1"
        ditl[10] = 0x5e32;    // "^2"
        ditl[0xb] = 0x5e33;   // "^3"
        ditl[0xc] = 0;        // item 2 placeholder handle
        ditl[0xd] = 0;
        ditl[0xe] = 104;      // item 2 Rect (104,140,124,210) — button 1
        ditl[0xf] = 140;
        ditl[0x10] = 124;
        ditl[0x11] = 210;
        ditl[0x12] = 0x400;   // type 0x04 button, dataLength 0
        ditl[0x13] = 0;       // item 3 placeholder handle
        ditl[0x14] = 0;
        ditl[0x15] = 104;     // item 3 Rect (104,30,124,100) — button 2
        ditl[0x16] = 30;
        ditl[0x17] = 124;
        ditl[0x18] = 100;
        ditl[0x19] = 0x400;   // type 0x04 button, dataLength 0
        ditl[0x1a] = 0;       // item 4 placeholder handle
        ditl[0x1b] = 0;
        ditl[0x1c] = 72;      // item 4 Rect (72,30,92,100) — button 3
        ditl[0x1d] = 30;
        ditl[0x1e] = 92;
        ditl[0x1f] = 100;
        ditl[0x20] = 0x400;   // type 0x04 button, dataLength 0
    }
}
