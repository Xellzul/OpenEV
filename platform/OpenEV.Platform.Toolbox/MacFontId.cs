namespace OpenEV.Platform.Toolbox;

// Classic Mac font family IDs — the values passed to TextFont (resolved by
// MacToolbox.ResolveFont). 0/3 are the standard system/Geneva faces; 20 and 2020 are
// the two custom faces the game selects.
public static class MacFontId
{
    public const int SystemFont = 0;      // Chicago (the classic system font)
    public const int Geneva     = 3;      // default UI text face
    public const int Times      = 20;     // 0x14 — the credits roll
    public const int Sillycon   = 2020;   // 0x7e4 — the bundled custom face (FOND 2020 / sfnt 9295)
}
