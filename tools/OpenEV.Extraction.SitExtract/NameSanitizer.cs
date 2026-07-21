namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// Maps classic Mac filenames to Windows-legal ones the same way the SheepShaver
/// extfs layer does in the repo's existing extracted tree: characters that are illegal
/// on the host (plus <c>%</c> itself, so the mapping stays reversible) become
/// uppercase <c>%XX</c> escapes — e.g. the classic custom-icon file <c>"Icon\r"</c>
/// is stored as <c>Icon%0D</c>. Windows reserved device names (CON, PRN, …) are not
/// escaped; none occur in classic Mac trees we care about.
/// </summary>
internal static class NameSanitizer
{
    private const string IllegalChars = "<>:\"/\\|?*%";

    public static string Sanitize(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (c < 0x20 || IllegalChars.Contains(c))
                sb.Append($"%{(int)c:X2}");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
