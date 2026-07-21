using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Systems;

// FUN_1005c654 — recursively spreads the legal-status ("kill") impact of an event
// outward from systemIndex across the connected systems, scaling the impact by
// 0.65 at each hop and weighting it by the relationship between the source
// government and each system's government.
//
// The decompile expresses every int->double conversion with the PowerPC magic
// idiom (CONCAT44 pack + bias subtract). Every PenaltyForColumn read below
// multiplies straight into an already-double expression, so C#'s own numeric
// promotion does that widening with no explicit cast needed. The one place
// precision is load-bearing is the final status-update subtraction, where the
// ASM narrows to single precision (fsubs) before truncating — see the comment
// at that site; the two (float) casts there are NOT redundant.
// Decompile: EV Override-11.c lines 38271-38424.
public static class PropagateSystemKillImpact
{
    // srcGovt = the event's source government (-1 = none); column = the legal-status penalty
    // column (1/2/3 = disable/board/destroy). Both are int-width on the decompile's stack but
    // only ever hold a short.
    public static void Run(double impact, short systemIndex, short srcGovt, short column)
    {
        // Stop if this system was already visited on the current flood.
        if (GalaxyMapGlobals.VisitedSystemFlags[systemIndex] != 0)
        {
            return;
        }
        GalaxyMapGlobals.VisitedSystemFlags[systemIndex] = 1;

        var sys = SystTable.Store[systemIndex];
        short sysGovt = sys.Govt;

        float impactFactor = GovtRelationImpactFactor(sysGovt, srcGovt, column);
        double delta = impactFactor * (float)impact;

        // Only apply (and recurse) when the impact is outside the dead band.
        if (MathConstants.One <= delta
            || delta <= EvoMath.Model.MathConstants.NegativeOne)
        {
            short current = GalaxyMapGlobals.SystemStatus(systemIndex);
            // ASM: fsubs current-as-float, delta-as-float -> ROUNDS to single precision
            // before the fctiwz truncate. Subtracting in double here (current and delta
            // are both exact as doubles) would skip that rounding step and can pick a
            // different truncated integer at a near-integer boundary — keep both
            // operands float-typed so the subtraction itself is single-precision.
            short newStatus = (short)(int)((float)current - (float)delta);
            if (newStatus > 32000) newStatus = 32000;
            if (newStatus < -32000) newStatus = -32000;
            GalaxyMapGlobals.SetSystemStatus(systemIndex, newStatus);

            impact *= ShipPhysicsConstants.ShipManeuverScale;
            foreach (short neighbor in sys.HyperLink)
            {
                if (neighbor != -1)
                {
                    Run(impact, neighbor, srcGovt, column);
                }
            }
        }
    }

    // FUN_1005c654 38302-38395 — weight the raw kill penalty by the relationship between the
    // source government (srcGovt) and the system's government (sysGovt), either of which may be
    // -1 (= none): ally/enemy/same-govt and the govt flag bits (Flags +0x20, bit 0 / bit 1) pick
    // the sign and the 0.5 / 0.25 scale, or pass the penalty through unscaled. Pure read of the
    // govt table — no side effects.
    private static float GovtRelationImpactFactor(short sysGovt, short srcGovt, short column)
    {
        double scale = MathConstants.Half;

        bool srcXenophobic = srcGovt != -1
            && (GameData.Governments[srcGovt].Flags & GovtFlags.Xenophobic) != 0;
        bool sysXenophobic = false;
        bool sysLawEnforcementEverywhere = false;
        if (sysGovt != -1)
        {
            sysXenophobic = (GameData.Governments[sysGovt].Flags & GovtFlags.Xenophobic) != 0;
            sysLawEnforcementEverywhere = (GameData.Governments[sysGovt].Flags & GovtFlags.LawEnforcementEverywhere) != 0;
        }

        float impactFactor;
        if (sysGovt == -1)
        {
            if (srcGovt == -1)
            {
                impactFactor = (float)(scale * GameData.Governments[0].PenaltyForColumn(column));
            }
            else if (!srcXenophobic)
            {
                impactFactor = (float)(MathConstants.Quarter * GameData.Governments[srcGovt].PenaltyForColumn(column));
            }
            else
            {
                impactFactor = (float)(scale * -GameData.Governments[srcGovt].PenaltyForColumn(column));
            }
        }
        else if (srcGovt == sysGovt)
        {
            impactFactor = (float)GameData.Governments[srcGovt].PenaltyForColumn(column);
        }
        else if (srcGovt == -1)
        {
            if (!sysXenophobic)
            {
                // _DAT_100822cc is a 4-byte float in the data segment (not a double,
                // unlike the govt-table shorts converted above) — use the typed
                // float epsilon directly rather than widening it.
                impactFactor = ShipStatConstants.NearestSearchEpsilon;
                if (sysLawEnforcementEverywhere)
                {
                    impactFactor = (float)(scale * GameData.Governments[sysGovt].PenaltyForColumn(column));
                }
            }
            else
            {
                impactFactor = (float)(scale * -GameData.Governments[sysGovt].PenaltyForColumn(column));
            }
        }
        else if (GameData.Governments[srcGovt].Ally == sysGovt
                 || srcGovt == GameData.Governments[sysGovt].Ally
                 || !srcXenophobic)
        {
            if (GameData.Governments[srcGovt].Enemy == sysGovt
                || srcGovt == GameData.Governments[sysGovt].Enemy
                || srcXenophobic)
            {
                impactFactor = (float)(scale * -GameData.Governments[sysGovt].PenaltyForColumn(column));
            }
            else if (GameData.Governments[srcGovt].Ally == sysGovt
                     || srcGovt == GameData.Governments[sysGovt].Ally)
            {
                impactFactor = (float)GameData.Governments[srcGovt].PenaltyForColumn(column);
            }
            else
            {
                impactFactor = (float)(scale * GameData.Governments[srcGovt].PenaltyForColumn(column));
            }
        }
        else
        {
            impactFactor = (float)(scale * -GameData.Governments[sysGovt].PenaltyForColumn(column));
        }
        return impactFactor;
    }
}
