namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;

// FUN_1006b3e0 — EV Override-11.c lines 44140-44179. For each active ship, recomputes its
// incoming-damage threat as the sum, over live mode-0 projectiles targeting it, of
// 0.5 × (weapon MassDamage + EnergyDamage).
public static class AccumulateIncomingDamageThreat
{
    public static void Run()
    {
        for (short shipIndex = 0; shipIndex < ShipTable.Count; shipIndex++)
        {
            var ship = ShipTable.Ships[shipIndex];
            if (ship.IsActive == 0)
                continue;

            ship.IncomingDamageThreat = 0;
            for (short projIndex = 0; projIndex < ProjectileTable.Count; projIndex++)
            {
                var proj = GameData.Projectiles[projIndex];
                if (0 < proj.LifeRemaining && shipIndex == proj.TargetSlot && proj.Mode == 0)
                {
                    var weapon = GameData.Weapons[proj.WeaponType];
                    ship.IncomingDamageThreat = (short)(int)(ShipStatConstants.Half
                        * (weapon.MassDamage + weapon.EnergyDamage) + ship.IncomingDamageThreat);
                }
            }
        }
    }
}
