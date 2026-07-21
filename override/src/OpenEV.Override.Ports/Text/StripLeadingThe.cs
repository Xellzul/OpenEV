namespace OpenEV.Override.Ports.Text;

// Port of FUN_1005bda0 (EV Override-11.c lines 37963-37995).
// The original strips a leading case-insensitive "the " from a Pascal string in
// a char[] scratch buffer; this port expresses the same test over a managed string.
public static class StripLeadingThe
{
    public static string Run(string s)
    {
        if (s.Length >= 4 &&
            char.ToLowerInvariant(s[0]) == 't' &&
            char.ToLowerInvariant(s[1]) == 'h' &&
            char.ToLowerInvariant(s[2]) == 'e' &&
            s[3] == ' ')
        {
            return s.Substring(4);
        }
        return s;
    }
}