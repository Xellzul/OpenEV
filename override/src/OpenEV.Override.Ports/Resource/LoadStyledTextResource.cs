using System.Text;
using OpenEV.Override.Ports.Misc;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// Port of FUN_10073690 (EV Override-11.c lines 47760-47873) — load a TEXT resource into a
// dialog item rect with the shareware-nag's '^0'..'^9' caret substitution.
//
// The Mac body staged the text in a STYLED TextEdit record: TEStyleNew over the dest rect,
// TEStyleInsert(*TEXT, size, styl) to apply the 'styl' resource's per-run fonts/sizes/faces,
// TECalText, then it walked the TE text replacing each '^N' caret token (via TESetSelect/
// TEDelete/TEInsert with the callback's resolved string), wrote the dest rect into the TE
// record's destRect/viewRect, and appended the TEHandle to the styled-TE list so update events
// (TEUpdate) redraw it.
//
// The port's styled TextEdit record chain is UNWIRED (TEStyleNew returns 0;
// TEStyleInsert/TECalText/TEUpdate/TEInsert are no-op shims), so this port takes the
// equivalent direct route: load the TEXT, parse the matching 'styl' resource's run table
// (per-run font/size/face + TE line metrics), resolve the '^N' carets (adjusting run
// starts across the replacements exactly as TE's delete/insert would), and register both
// for the open dialog; RedrawDialog replays it through the styled TETextBox renderer
// (MacToolbox.DrawStyledTextBox) so it survives every dialog redraw.
public static class LoadStyledTextResource
{
    private const uint StylType = 0x7374796c;   // 'styl' — the TE style-scrap resource

    // ShowSharewareNagDialog's GetDialogItem rect out is a short[4] {top,left,bottom,right}.
    public static int Run(int resId, short[] destRect, int callbackProc)
    {
        // Load the TEXT resource (raw text — a TEXT resource is NOT length-prefixed). Mac
        // resource text is Mac-Roman (curly quotes/apostrophes/dashes are high bytes), so
        // decode it as such — Windows-1252 would mangle them.
        int textHandle = MacToolbox.GetResource(MacResType.Text, resId);
        if (textHandle == 0) return -192;   // 0xffffff40 resNotFound (faithful: TEXT absent)
        byte[] raw = MacToolbox.HandleToBytes(textHandle);
        string text = MacToolbox.MacRomanToString(raw);

        // The paired 'styl' resource (same id): TE style scrap — int16 nRuns, then
        // 20-byte runs {int32 startChar; int16 height; int16 ascent; int16 fontID;
        // Style face + pad byte; int16 size; RGBColor}. ASM: a missing 'styl' aborts the
        // whole call (TEDispose the TE record, return resNotFound, never register the
        // text) — ported faithfully below.
        int stylHandle = MacToolbox.GetResource(StylType, resId);
        if (stylHandle == 0) return -192;   // 0xffffff40 resNotFound (faithful: loc_73798 abort)
        // DEVIATION (faithful): the ASM never validates 'styl' CONTENT — a present-but-truncated
        // resource just feeds whatever bytes exist to TEStyleInsert, genuinely-unpreservable
        // PowerPC UB with no style-run renderer in the port to reproduce it. The port degrades to plain
        // unstyled text instead (ParseStyl returns null on a too-short/truncated run table).
        var runs = ParseStyl(stylHandle);

        // Every '^0'..'^9' caret token is found+removed unconditionally (ASM: TESetSelect/
        // TEDelete/TEInsert run outside the `if (param_3 != 0)` guard); the guard wraps ONLY
        // the ResolveShareWarePlaceholder call that supplies the replacement (selector = the
        // decompile's char − 0x2f). See SubstituteCaretTokens for the callbackProc==0 case.
        text = SubstituteCaretTokens(text, runs, callbackProc);

        // Register for the frontmost dialog (the nag); RedrawDialog replays it.
        MacToolbox.AddDialogStyledText(MacToolbox.CurrentDialogId, destRect, text, runs);
        return 0;   // noErr
    }

    private static MacToolbox.StyledRun[]? ParseStyl(int handle)
    {
        byte[] data = MacToolbox.HandleToBytes(handle);
        if (data is null || data.Length < 2) return null;
        int runCount = BigEndian.ReadInt16(data, 0);
        if (runCount <= 0 || data.Length < 2 + runCount * 20) return null;
        var runs = new MacToolbox.StyledRun[runCount];
        for (int i = 0; i < runCount; i++)
        {
            int runOffset = 2 + i * 20;
            runs[i] = new MacToolbox.StyledRun(
                start: BigEndian.ReadInt32(data, runOffset), font: BigEndian.ReadInt16(data, runOffset + 8),
                size: BigEndian.ReadInt16(data, runOffset + 12), face: data[runOffset + 10],
                height: BigEndian.ReadInt16(data, runOffset + 4), ascent: BigEndian.ReadInt16(data, runOffset + 6));
        }
        return runs;
    }

    /// Replace each '^N' token, and shift the style-run start offsets across the
    /// length changes — the TESetSelect/TEDelete/TEInsert sequence the Mac ran kept
    /// the style table aligned the same way.
    /// DEVIATION (faithful): single forward pass. The Mac's in-place TEDelete/TEInsert
    /// does NOT advance past a substitution (ASM: loc_738B4 falls through to loc_738F0
    /// without incrementing sVar7), so it re-scans from the same index and would
    /// recursively re-substitute a resolved value that itself contained "^N". This pass
    /// never re-examines inserted text — harmless for the actual resolved strings
    /// (digits / fixed localized text, never containing '^'), but a structural
    /// simplification of the ASM's control flow.
    private static string SubstituteCaretTokens(string text, MacToolbox.StyledRun[]? runs, int callbackProc)
    {
        var sb = new StringBuilder(text.Length + 16);
        int[]? newStarts = runs is null ? null : new int[runs.Length];
        int next = 0;   // next run whose (pre-substitution) start we haven't mapped yet
        for (int i = 0; i < text.Length; i++)
        {
            if (runs is not null)
                while (next < runs.Length && runs[next].Start <= i)
                { newStarts![next] = sb.Length + (runs[next].Start - i); next++; }
            char c = text[i];
            if (c == '^' && i + 1 < text.Length && text[i + 1] >= '0' && text[i + 1] <= '9')
            {
                // DEVIATION (faithful): ASM always deletes the "^N" token here; the
                // `param_3 != 0` guard covers only this resolve call. When callbackProc == 0
                // the Mac inserted whatever was left in the never-initialized stack buffer
                // local_128 — genuinely unpreservable PowerPC UB (and unreachable: both call
                // sites, in ShowSharewareNagDialog, always pass a non-null callback UPP) —
                // the port substitutes empty string instead of stack garbage.
                if (callbackProc != 0)
                {
                    short selector = (short)(text[i + 1] - '0' + 1);   // '^0' → selector 1 … '^9' → 10
                    sb.Append(MacToolbox.PascalToString(ResolveShareWarePlaceholder.Run(selector)));
                }
                i++;   // consume the digit
            }
            else
            {
                sb.Append(c);
            }
        }
        if (runs is not null)
        {
            while (next < runs.Length) { newStarts![next] = sb.Length; next++; }
            for (int r = 0; r < runs.Length; r++)
                runs[r] = new MacToolbox.StyledRun(newStarts![r], runs[r].Font, runs[r].Size,
                                                   runs[r].Face, runs[r].Height, runs[r].Ascent);
        }
        return sb.ToString();
    }
}
