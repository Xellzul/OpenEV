using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// The styled-text '^N' caret-substitution callback (UPP 0x10072eac) the shareware-nag
// TEXT/styl loader invokes per token. The Mac form took a Str255 out-pointer and
// BlockMoveData'd the result into it; the port returns the Pascal byte[] directly —
// LoadStyledTextResource.SubstituteCaretTokens splices it into the nag text (the live
// "^1 days" / "^2 hours" / reg-code substitutions in DLOG 900's message body).
public static class ResolveShareWarePlaceholder
{
    public static byte[] Run(short selector)
    {
        byte[] resultString = new byte[0x100]; // Pascal-string result (was BlockMoveData'd 0x100 bytes into the Str255 out)

        // DEVIATION (faithful): the decompile's switch has no default — an out-of-range
        // selector falls straight through to the BlockMoveData with whatever was left on
        // the Mac stack (genuinely unpreservable UB). Selectors 8-10 ARE reachable calls,
        // not just theoretical: SubstituteCaretTokens maps caret digit D to selector D+1
        // for every '^0'..'^9', so '^7'/'^8'/'^9' would land here (no known TEXT/styl
        // resource is confirmed to emit them, but the call path itself is live). The
        // port's zero-initialized array yields an empty Pascal string instead, same
        // shape as every in-range case's "length 0" starting point.
        switch (selector)
        {
            case 1:
                MacToolbox.GetIndString(resultString, 900, 4);
                break;
            case 2:
                GetDaysSinceInstall.Run(out int days2);
                MacToolbox.NumToString(days2, resultString);
                break;
            case 3:
                GetInstallHours.Run(out int hours3);
                MacToolbox.NumToString(hours3, resultString);
                break;
            case 4:
                resultString[0] = 0;
                GetDaysSinceInstall.Run(out int days4);
                if (days4 != 1)
                {
                    resultString[0] = (byte)(resultString[0] + 1);
                    resultString[1] = (byte)'s'; // pluralize: append 's' when count != 1
                }
                break;
            case 5:
                resultString[0] = 0;
                GetInstallHours.Run(out int hours5);
                if (hours5 != 1)
                {
                    resultString[0] = (byte)(resultString[0] + 1);
                    resultString[1] = (byte)'s'; // pluralize: append 's' when count != 1
                }
                break;
            case 6:
                GetRegistrationCode.Run(out int code6);
                MacToolbox.NumToString(code6, resultString);
                break;
            case 7:
                resultString[0] = 0;
                GetRegistrationCode.Run(out int code7);
                if (code7 != 1)
                {
                    resultString[0] = (byte)(resultString[0] + 1);
                    resultString[1] = (byte)'s'; // pluralize: append 's' when count != 1
                }
                break;
        }
        return resultString;
    }
}
