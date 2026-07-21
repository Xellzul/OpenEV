using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenEV.Platform.Imaging;

// Host-side loader for a classic-Mac UI face as a SoftwareFont — the one copy of a chain
// every app (Override, Classic, Register) needs, since Mac OS system fonts (Geneva 3,
// Chicago 0) aren't in the game's resource fork:
//   1. Outline base — a real user TTF (e.g. geneva.ttf) dropped into the app's Fonts folder,
//      else a bundled FREE outline embedded in the calling assembly (no proprietary face ships).
//   2. Local *-N.bdf 1-bit strikes in that folder (a user's own extract) — pixel-exact at their
//      native size; the first one seeds the font if every outline load failed.
//   3. A bundled free strike at the size the UI draws, added only when no local strike covered it.
// Returns the built font (null if nothing loaded) plus a human Source label; failures are logged
// under `tag` and never throw.
public static class SoftwareFontLoader
{
    public sealed record Spec(
        string UserOutlineFile,      // e.g. "geneva.ttf"
        string UserOutlineLabel,     // Source label when the user file loads
        string EmbeddedOutline,      // embedded resource name, e.g. "Grand9K-Pixel.ttf"
        string EmbeddedOutlineLabel, // Source label when the bundled outline loads
        string LocalStrikeGlob,      // e.g. "Geneva-*.bdf"
        string EmbeddedStrike,       // embedded strike name, e.g. "Grand9K-9.bdf"
        int EmbeddedStrikeSize);     // the size the bundled strike covers (guards step 3)

    public static (SoftwareFont? Font, string Source) Load(
        Spec spec, string fontsDir, Assembly embeddedAssembly, string tag)
    {
        SoftwareFont? font = null;
        string source = "none";

        string userOutline = Path.Combine(fontsDir, spec.UserOutlineFile);
        if (File.Exists(userOutline))
        {
            try { font = new SoftwareFont(File.ReadAllBytes(userOutline)); source = spec.UserOutlineLabel; }
            catch { font = null; }
        }
        if (font is null)
        {
            try { font = new SoftwareFont(LoadEmbeddedBytes(embeddedAssembly, spec.EmbeddedOutline)); source = spec.EmbeddedOutlineLabel; }
            catch (Exception ex) { Console.WriteLine($"{tag}: bundled {spec.EmbeddedOutline} failed: {ex.Message}"); }
        }

        if (Directory.Exists(fontsDir))
        {
            foreach (var bdf in Directory.GetFiles(fontsDir, spec.LocalStrikeGlob))
            {
                try
                {
                    var strike = MacBitmapFont.FromBdfFile(bdf);
                    if (font is null) { font = SoftwareFont.FromStrike(strike); source = Path.GetFileName(bdf); }
                    else font.AddStrike(strike);
                }
                catch (Exception ex) { Console.WriteLine($"{tag}: strike {Path.GetFileName(bdf)} failed: {ex.Message}"); }
            }
        }

        // Bundled strike — only when no local strike already covered this size (font-null seeds it,
        // reachable only if every outline load above failed too).
        if (font is null || font.StrikeLineHeight(spec.EmbeddedStrikeSize) is null)
        {
            try
            {
                var strike = MacBitmapFont.FromBdfBytes(LoadEmbeddedBytes(embeddedAssembly, spec.EmbeddedStrike));
                if (font is null) { font = SoftwareFont.FromStrike(strike); source = spec.EmbeddedStrike; }
                else font.AddStrike(strike);
            }
            catch (Exception ex) { Console.WriteLine($"{tag}: bundled {spec.EmbeddedStrike} strike failed: {ex.Message}"); }
        }

        return (font, source);
    }

    // Raw bytes of a font embedded in the calling assembly (Fonts\*, EmbeddedResource).
    private static byte[] LoadEmbeddedBytes(Assembly asm, string fileName)
    {
        string? resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.Ordinal));
        if (resName is null) throw new FileNotFoundException($"embedded resource not found: {fileName}");
        using var s = asm.GetManifestResourceStream(resName)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
