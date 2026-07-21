namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;

// FUN_100616c8 (EV Override-11.c 40793-40808): a ship class's skill-variation multiplier —
// 0.01 × ((100 − skill) + rand(2·skill + 1)), rounded to float precision.
public static class SkillVariationRoll
{
    public static double Run(short shipClassIndex)
    {
        short skill = Core.Model.GameData.ShipClasses[shipClassIndex].SkillLevel;
        short randomRoll = (short)SeedEvoRng.Run((short)(skill * 2 + 1));
        return (double)(float)(ShipStatConstants.SkillVariationScale * ((100 - skill) + randomRoll));
    }
}
