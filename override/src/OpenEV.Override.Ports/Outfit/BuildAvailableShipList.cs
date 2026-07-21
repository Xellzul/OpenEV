using System;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_1005fb30 (EV Override-11.c line 39887). Builds the shipyard's
// "available ship" row map for the spöb the player is landed on: walks every ship
// class, decides which are buyable here, and packs their indices into
// ShipyardState.AvailableRowIndex (-1 = empty slot).
public static class BuildAvailableShipList
{
    public static void Run(SpobRec spob)
    {
        short[] rows = ShipyardState.AvailableRowIndex;
        Array.Fill(rows, (short)-1, 0, GameData.ShipClasses.Length);

        short outputCount = 0;
        for (int classIndex = 0; classIndex < GameData.ShipClasses.Length; classIndex++)
        {
            ShipClassRecord cls = GameData.ShipClasses[classIndex];
            // CheatShowAll forces every class visible.
            if (IsBuyableHere(spob, cls) || WorldState.CheatShowAll != 0)
            {
                rows[outputCount++] = (short)classIndex;
            }
        }
    }

    private static bool IsBuyableHere(SpobRec spob, ShipClassRecord cls)
    {
        // Tech gate: the spöb's tech level must reach the class, OR one of its three
        // special-tech slots must match the class tech exactly.
        bool buyable = spob.TechLevel >= cls.TechLevel
                       || Array.IndexOf(spob.SpecialTech, cls.TechLevel) >= 0;

        // Mission-control bit gate (-1 = ungated).
        short bit = cls.MissionBit;
        if (bit != -1)
        {
            // < 1000: the bit must be SET. >= 1000: the (bit-1000) bit must be CLEAR.
            bool bitAllows = bit < 1000 ? ControlBits.Get(bit) != 0
                                        : ControlBits.Get(bit - 1000) == 0;
            // Buy-escort mode additionally hides every mission-gated class.
            buyable = buyable && bitAllows && ShipyardState.EscortMode == 0;
        }

        return buyable;
    }
}
