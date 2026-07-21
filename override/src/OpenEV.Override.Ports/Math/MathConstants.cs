namespace OpenEV.Override.Ports.EvoMath;

// PEF data-segment math/physics double constants, migrated to managed C# literals —
// double-width per the decompile reads.
public static class MathConstants
{
    // Pooled scalars named by value — each cell is read for different roles or across
    // unrelated subsystems, so a use-derived name would mislead (cf. OnePercent below).
    public const double Half = 0.5;  // 0x10082278
    public const double Quarter = 0.25; // 0x10082270
    public const double One = 1.0;  // 0x10082280

    // Ambient-dust render offsets (Systems.Asteroids).
    public const double DustOffsetScale = -0.01; // 0x10082330
    public const double DustXScale = 0.7;   // 0x10082338
    // 0.01, shared by asteroid dust spread and the escort wage (PayEscortWages).
    public const double OnePercent = 0.01;  // 0x10082340
}
