using System.Text;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.EvoData;

/// <summary>
/// EV Override's reading of classic-Mac OSType codes. Its gameplay resources use one
/// accented Mac Roman letter inside the 4-char type code (shïp, wëap, spöb, sÿst, …) so
/// third-party plug-ins can't collide with the engine's own types. The game's Resource
/// Manager dispatch keys off the plain ASCII spelling ("ship", "weap", "spob"), so the
/// accented code is folded back to ASCII here.
/// <para>
/// This is EV Override knowledge, not a property of the generic Mac resource-fork
/// container, so it lives in the data layer rather than in OpenEV.Platform.ResourceFork.
/// </para>
/// </summary>
public static class EvoResourceType
{
    private const int Windows1252CodePage = 1252;

    // Decoding the raw OSType bytes as Windows-1252 reproduces the classic Mac Resource
    // Manager's behaviour under a US/Western encoding; NormalizeTypeCode then folds the
    // resulting mis-decoded accented chars back to the ASCII code. ModuleInit.cs registers
    // CodePagesEncodingProvider at module load.
    private static readonly Encoding Win1252 = Encoding.GetEncoding(Windows1252CodePage);

    /// <summary>
    /// The resource's 4 OSType bytes as the ASCII 4-char code the game dispatches on
    /// (e.g. "ship", "snd ", "STR#"). Folds EV Override's accented type codes (shïp → ship).
    /// </summary>
    public static string EvoType(this ForkResource res) => Normalize(res.RawType);

    /// <summary>Fold a packed big-endian OSType to its ASCII 4-char dispatch code.</summary>
    public static string Normalize(uint rawType)
    {
        Span<byte> raw = stackalloc byte[4]
        {
            (byte)(rawType >> 24), (byte)(rawType >> 16), (byte)(rawType >> 8), (byte)rawType,
        };
        return NormalizeTypeCode(Win1252.GetString(raw));
    }

    // MacRoman bytes, when interpreted as Windows-1252, produce these mis-decoded chars.
    // Verbatim from the former ForkResource.NormalizeTypeCode so the game's string-switch
    // dispatch behaves identically after moving the EVO mapping out of the resource-fork lib.
    private static string NormalizeTypeCode(string raw) => raw switch
    {
        "sh•p" => "ship",
        "w‘ap" => "weap",
        "oŸtf" => "outf",
        "p‘rs" => "pers",
        "dœde" => "dude",
        "dŸde" => "dude",
        "fl‘t" => "flet",
        "g¿vt" => "govt",
        "gšvt" => "govt",
        "sØst" => "syst",
        "sp¿b" => "spob",
        "spšb" => "spob",
        "n‘bu" => "nebu",
        "šops" => "oops",
        "m•sn" => "misn",
        "jŸnk" => "junk",
        "d‘sc" => "desc",
        "d‘qt" => "deqt",
        "y‘Š¨" => "year",
        "sp•n" => "spin",
        "shŠn" => "shan",
        "cršn" => "cron",
        "ršid" => "roid",
        "chŠr" => "char",
        "cšlr" => "colr",
        "bššm" => "boom",
        "rl‘D" => "rleD",
        "rl‘8" => "rle8",
        // Default: keep the raw 4-char code verbatim. Stripping spaces here used to
        // eat the trailing space in 'snd ' and 'STR ', dropping every sound and string.
        _ => raw
    };
}
