namespace OpenEV.Override.Ports.Ship.Model;

// Managed (real C# literal) values for the ship physics scale/divisor constants in
// the PEF data segment — the EffectiveSpeed / EffectiveAccel / EffectiveShieldRecharge /
// EffectiveManeuver family. Mirrors Ports/Managed/MathConstants: the migrated,
// "doesn't touch EvoMemory" home, kept separate from the slot-ADDRESS registry in
// Ports/Ship/ShipStatConstants.cs.
//
// Each value is the EXACT IEEE-754 value the data segment holds, extracted by
// decompressing the PEF data section (the contiguous block 0x10082288..0x100822b0).
// Widths match the per-site read width in the decompile (float vs double); every
// reader is migrated to the managed constants below (the old EvoMemory source
// addresses were removed along with EvoMemory itself — see OriginalGameStateTotalBytes).
public static class ShipPhysicsConstants
{
    public const float ShipStatFinalScale = 1.5f;      // _DAT_10082288 (float)  final non-combat speed multiplier
    public const double ShipSpeedModDivisor = 100.0;     // _DAT_10082290 (double) Speed-mod outfit divisor (sole use: EffectiveSpeed)
    public const float ShipSpeedScaleAlt = 0.333f;    // _DAT_10082298 (float)  alternate (tractored) speed scale
    public const float ShipSpeedAccelScale = 2.0f;      // _DAT_1008229c (float)  speed/accel unit scale + pers boost
    public const double ShipAccelDivisor = 10000.0;   // _DAT_100822a0 (double) Acceleration-mod outfit divisor
    public const double NonPlayerManeuverScale = 0.333;     // _DAT_100822a8 (double) NPC maneuver multiplier
    public const double ShipManeuverScale = 0.65;      // _DAT_100822b0 (double) maneuver/recharge→angle + kill-impact scale
}
