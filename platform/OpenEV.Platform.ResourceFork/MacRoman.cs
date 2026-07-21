using System.Text;

namespace OpenEV.Platform.ResourceFork;

/// <summary>
/// Decodes classic Mac Roman (code page 10000, a.k.a. x-mac-roman) bytes. EVO resource OSType
/// codes and names use Mac Roman high bytes (ë=0x91, ö=0x9a, ü=0x9f, ÿ=0xd8, ï=0x95, é=0x8e),
/// which do NOT line up with Latin-1: decoding "nëbu" as Latin-1 turns 0x91 into the U+0091
/// control char (no glyph), so the accented letter silently vanishes in the UI.
/// </summary>
public static class MacRoman
{
    // Classic Mac Roman a.k.a. x-mac-roman.
    private const int MacRomanCodePage = 10000;

    // ModuleInit.cs registers CodePagesEncodingProvider at module load; no per-call init needed.
    private static readonly Encoding Enc = Encoding.GetEncoding(MacRomanCodePage);

    public static string GetString(ReadOnlySpan<byte> bytes) => Enc.GetString(bytes);
}
