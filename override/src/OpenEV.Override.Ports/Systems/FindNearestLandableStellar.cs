using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_100597a8 (EV Override-11.c 36807-36847) — the nearest landable,
// visible stellar in the ship's current system, by squared distance over the
// system's 4 stellar links, or -1.
public static class FindNearestLandableStellar
{
    public static int Run(ShipRec ship)
    {
        int bestStellar = -1;
        float bestDistSq = ShipStatConstants.NearestSearchMaxDist;
        for (short linkIndex = 0; linkIndex < SystRecord.StellarLinkCount; linkIndex = (short)(linkIndex + 1))
        {
            short stellarId = SystTable.SpobLink(ship.CurrentSystem, linkIndex);
            if (stellarId == -1)
                continue;
            var spob = GameData.Spobs[stellarId];
            if (spob.Visible == 0 || (spob.Flags & 1) == 0)
                continue;
            // spob X/Y are short coords; ship position is float.
            double dx = EvMath.FloatAbs((double)(ship.PosX - (float)spob.XPos));
            double dy = EvMath.FloatAbs((double)(ship.PosY - (float)spob.YPos));
            float distSq = (float)(dx * dx + (double)(float)(dy * dy));
            // The second clause tests the running best (the -1 "none found"
            // sentinel), not the candidate — so the first valid stellar is accepted.
            // Faithful to the decompile; do not rewrite it to compare distSq.
            if (distSq < bestDistSq || bestDistSq < ShipStatConstants.NearestSearchEpsilon)
            {
                bestStellar = stellarId;
                bestDistSq = distSq;
            }
        }
        return bestStellar;
    }
}
