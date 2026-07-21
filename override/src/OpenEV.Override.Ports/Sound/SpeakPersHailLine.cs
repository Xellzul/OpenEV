using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 45286-45304.
//
// Pers hail: chime (snd 154), load STR 4999+hailQuoteId (falling back to the STR#
// 0x1bbd pers-hail list), expand the %-tokens in place, then enqueue the line
// as HUD chatter. Despite the name it never reaches the speech synth itself —
// SpeakText is gated separately (faithful to FUN_1006f08c).
public static class SpeakPersHailLine
{
    // Every real caller passes PersRecord.HailQuote (a resource ID), never a pers
    // table index/identity — named for the value, not the misleading original "persId".
    public static void Run(int hailQuoteId)
    {
        SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);   // snd 154

        // Hail line into the shared text scratch: STR' hailQuoteId+4999, else the
        // STR# 0x1bbd pers-hail list entry hailQuoteId (the p2cstr/c2pstr staging is gone).
        TextScratch.Text = TryLoadStr.RunString((short)(hailQuoteId + 4999))
            ?? MacToolbox.GetIndString(0x1bbd, (short)hailQuoteId);

        // FUN_1004f078(0, -1): expand the %-tokens in the scratch string (-1 = no mission context).
        SubstituteMissionDescTags.Run(0, -1);

        // The chatter-colour arg is `*(local_2c - 0x7adc)` in the decompile — a
        // decompile TOC-spill artifact; GameToc-0x7adc is the chatter-text RGBColor ptr cell
        // 0x10080b84 -> UiColors.ChatterText (same routing as every FUN_100679b0 site).
        // The initial transcription mistook the TOC-spill for an uninitialized local and read near-null.
        EnqueueChatterEvent.Run(TextScratch.Text, 420, 0, 12, UiColors.ChatterText, 0, 0);
        return;
    }
}
