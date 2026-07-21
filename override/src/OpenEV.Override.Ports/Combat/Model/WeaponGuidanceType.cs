namespace OpenEV.Override.Ports.Combat.Model;

// Weapon GuidanceType (wëap +0x06) — how the shot behaves in flight. Verified names from the
// editor TMPL schema (Schemas.More.WeapGuidanceTypes). The major dispatch lives in WeaponSlotTick
// and SpawnSpecialWeaponShip. The loader rewrites the unused value 2 to 1 (HomingWeapon), so it
// never reaches runtime; out-of-range values keep their raw number and simply won't match a member.
public enum WeaponGuidanceType : short
{
    UnguidedProjectile = -1,
    BeamWeapon = 0,
    HomingWeapon = 1,
    TurretedBeam = 3,
    TurretedUnguided = 4,
    FreefallBomb = 5,
    FreeflightRocket = 6,
    FrontQuadrantTurret = 7,
    RearQuadrantTurret = 8,
    CarriedShip = 99,   // Ammo/link field = the carried ship-class id
}
