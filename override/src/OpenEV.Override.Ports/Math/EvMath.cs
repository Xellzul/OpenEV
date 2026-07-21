using System;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship.Model;
using TrigState = OpenEV.Override.Ports.EvoMath.Model.TrigState;

namespace OpenEV.Override.Ports.EvoMath;

// Consolidated EV Override math routines, migrated from the per-FUN Math/ ports
// into managed C#. The trig tables and data-segment math constants live in
// Model/TrigState and Model/MathConstants; the stateful routines return their
// value instead of writing the Mac shared result slot.
public static class EvMath
{
    // FUN_100581a0 / FUN_100581e0 — sin / cos of a heading (degrees), looked up in
    // the TrigState tables (filled by InitTrigTables) after normalizing to 0..359.
    // Callers branch to FUN_10058198 / FUN_100581d8 (the decompile's thunk_FUN_100581a0/e0);
    // the original wrote a shared result slot, managed code returns the value.
    public static float Sin360(short degree) => TrigState.Sin(Normalize360(degree));
    public static float Cos360(short degree) => TrigState.Cos(Normalize360(degree));

    private static short Normalize360(short degree)
    {
        while (degree < 0) degree = (short)(degree + 360);
        while (degree > 359) degree = (short)(degree - 360);
        return degree;
    }

    // FUN_10061064 (EV Override-11.c 40585-40598) — angular delta between two
    // headings (0..359): |a-b|, flipped to 360-|a-b| when the two headings fall in
    // different 0..179 / 180..359 halves.
    public static int AngleDelta(short headingA, short headingB)
    {
        int delta = Math.Abs((int)headingA - (int)headingB);
        if ((headingA >= 180) != (headingB >= 180))
            delta = 360 - delta;
        return delta;
    }

    // FUN_10058a60 (EV Override-11.c 36444-36453) — squared distance between two
    // points, |dx|²+|dy|² with dy² and the whole sum each rounded to float
    // (fmuls + fmadds). Ship-pair overload.
    public static double DistanceSquared(ShipRec a, ShipRec b)
    {
        double dx = FloatAbs((double)(a.PosX - b.PosX));
        double dy = FloatAbs((double)(a.PosY - b.PosY));
        return (double)(float)(dx * dx + (double)(float)(dy * dy));
    }

    // Overload taking loose float pairs (e.g. the {0,0} system-centre origin) — same
    // float rounding as the ship-pair form; do not drop either (float) cast (the
    // rounding decides which side of the HyperOut/DefendRetreat home-distance checks a ship lands).
    public static double DistanceSquared(float ax, float ay, float bx, float by)
    {
        double dx = FloatAbs((double)(ax - bx));
        double dy = FloatAbs((double)(ay - by));
        return (double)(float)(dx * dx + (double)(float)(dy * dy));
    }

    // FUN_10058a0c (EV Override-11.c 36423-36440) — clamp the ship's {VelX,VelY} into
    // [-limit, limit]. The final branch is asymmetric (y's lower bound is an early
    // return), preserved from the original. In ApplyShipDamage the decompile drops
    // both args (bare FUN_10058a0c(param_1+2)); limit is the preceding EffectiveSpeed
    // double return, param_1+2 is the ship velocity pair.
    public static void ClampVector(double limit, ShipRec ship)
    {
        if (limit < ship.VelX) ship.VelX = (float)limit;
        if (ship.VelX < -limit) ship.VelX = (float)-limit;
        if (limit < ship.VelY) ship.VelY = (float)limit;
        if (ship.VelY >= -limit) return;
        ship.VelY = (float)-limit;
    }

    // ref-float form for typed projectile records.
    public static void ClampVector(double limit, ref float x, ref float y)
    {
        if (limit < x) x = (float)limit;
        if (x < -limit) x = (float)-limit;
        if (limit < y) y = (float)limit;
        if (y >= -limit) return;
        y = (float)-limit;
    }

    // FUN_100589a0 (EV Override-11.c 36404-36419) — heading from point 2 to point 1
    // via the atan2 lookup: Atan2(x1-x2, y1-y2).
    public static int HeadingBetween(float x1, float y1, float x2, float y2)
        => Atan2((double)(x1 - x2), (double)(y1 - y2));

    // FUN_10058218 (EV Override-11.c 36159-36210) — integer atan2: the game heading
    // (0..359) of the vector (x, y) via the precomputed atan lookup table. The
    // quadrant-split boundary (0.0) and the ratio→index scale (100.0) are
    // data-segment floats in Model.MathConstants.
    public static int Atan2(double x, double y)
    {
        double zero = Model.MathConstants.Atan2QuadrantBoundary;

        // First-octant ratio: min(|x|,|y|) / max(|x|,|y|), in [0, 1].
        double absX = FloatAbs(x);
        double absY = FloatAbs(y);
        double ratio = absY <= absX
            ? FloatAbs((double)(float)(y / x))
            : FloatAbs((double)(float)(x / y));

        // Scale the ratio to an atan-table index, clamped to 0..1023 (1024 entries).
        // The (float) round matches the ASM's single-precision fmuls (fmuls f0,f0,f29)
        // before fctiwz; the decompile dropped the frsp and rendered a plain double multiply.
        // ASM wins over the decompile - keep the (float) cast (single-round can shift the
        // index by 1 at a boundary vs a double multiply; that is the binary's behavior).
        short index = (short)(int)(float)(Model.MathConstants.Atan2RatioScale * ratio);
        if (index < 0) index = 0;
        if (index > 1023) index = 1023;

        short atan = TrigState.Atan(index);
        int angle = atan < 0 ? -(int)atan : atan;

        // Fold the first-octant angle back out into the full circle by quadrant.
        if (absX < absY) angle = 90 - angle;    // steep half
        if (x < zero && zero <= y) angle = 180 - angle;   // Q2
        if (x < zero && y < zero) angle += 180;          // Q3
        if (zero <= x && y < zero) angle = -angle;        // Q4

        // Re-base by -90 into the game's heading convention, wrapped to 0..359.
        int result = angle - 90;
        if ((short)result < 0) result = angle + 270;
        if ((short)result > 359) result -= 360;
        return result;
    }

    // FUN_10058970 (EV Override-11.c 36393-36400) — float-style absolute value:
    // negate when < 0, through a single-precision round ((double)(float)(-1.0*value)),
    // not System.Math.Abs.
    public static double FloatAbs(double value)
    {
        if (value < 0.0) value = (double)(float)(-1.0 * value);
        return value;
    }

    // FUN_10072c44 (EV Override-11.c 47370-47379) — rotate a 32-bit value left by
    // shiftBits. The original rotated a uint* in place; managed code returns the
    // value (the caller assigns it back).
    public static uint RotateLeft(uint value, short shiftBits)
        => value << ((int)shiftBits & 0x3f) | value >> (32 - (int)shiftBits & 0x3f);

    // FUN_10072c1c (EV Override-11.c 47357-47365) — rotate a 32-bit value right by
    // shiftBits. See RotateLeft.
    public static uint RotateRight(uint value, short shiftBits)
        => value >> ((int)shiftBits & 0x3f) | value << (32 - (int)shiftBits & 0x3f);

    // FUN_1007c324 (EV Override-11.c 53283-53288) — do two Mac Rects overlap? Each
    // rect is {top, left, bottom, right} on the SpriteNode.
    public static bool MacRectsOverlap(SpriteNode a, SpriteNode b)
        => a.RectTop < b.RectBottom &&
           b.RectTop < a.RectBottom &&
           a.RectLeft < b.RectRight &&
           b.RectLeft < a.RectRight;

    // FUN_100586f0 (EV Override-11.c 36314-36322) — offset the {x,y} pair by
    // magnitude along heading (sin → +x, cos → −y).
    public static void OffsetByHeading(double magnitude, int heading, ref float x, ref float y)
    {
        x = (float)((double)Sin360((short)heading) * magnitude + (double)x);
        y = -(float)((double)Cos360((short)heading) * magnitude - (double)y);
    }

    // FUN_10058770 (EV Override-11.c 36328-36386) — nudge a ship's velocity one accel
    // step toward heading (sin → +x, −cos → +y), per axis, but only on an axis where
    // the ship is still under maxSpeed magnitude or is currently moving the wrong way
    // along it.
    public static void AccelerateAlongHeading(double accel, double maxSpeed, int heading, ShipRec ship)
    {
        var sinDir = Sin360((short)heading);
        var cosDir = Cos360((short)heading);

        // Per-axis max-speed cap and the signed accel step (products rounded to float, per fmuls).
        double speedCapX = FloatAbs((double)(float)((double)sinDir * maxSpeed));
        double speedCapY = FloatAbs((double)(float)((double)cosDir * maxSpeed));
        var accelX = (float)((double)sinDir * accel);
        var accelY = (float)(-(double)cosDir * accel);

        // Accelerate an axis unless it is already at/over the cap AND already moving that way.
        if (FloatAbs((double)ship.VelX) < FloatAbs((double)(float)speedCapX) || SignOf(ship.VelX) != SignOf(accelX))
            ship.VelX += accelX;
        if (FloatAbs((double)ship.VelY) < FloatAbs((double)(float)speedCapY) || SignOf(ship.VelY) != SignOf(accelY))
            ship.VelY += accelY;
    }

    // +1 for zero/positive, -1 for negative (the original's `0.0 <= v` test, PPC bge).
    private static short SignOf(double v) => 0.0 <= v ? (short)1 : (short)-1;

    // FUN_10058064 (EV Override-11.c 36055-36092) — fill the managed TrigState
    // sin/cos/tan/atan tables at boot. The sin/cos/tan loop single-rounds the index
    // to float (fsubs) before the deg→rad scale; the atan loop keeps it double (fsub).
    public static void InitTrigTables()
    {
        double degToRad = Model.MathConstants.DegToRad;
        double atanInput = Model.MathConstants.AtanInput;
        double atanOutput = Model.MathConstants.AtanOutput;

        for (int i = 0; i < TrigState.TableSize; i++)
        {
            double angleRad = (double)(float)(degToRad * (double)(float)(double)i);
            TrigState.SinTable[i] = (float)MacToolbox.sin(angleRad);
            TrigState.CosTable[i] = (float)MacToolbox.cos(angleRad);
            TrigState.TanTable[i] = (float)MacToolbox.tan(angleRad);
        }
        for (int i = 0; i < TrigState.AtanTable.Length; i++)
        {
            double result = MacToolbox.atan(atanInput * (double)i);
            TrigState.AtanTable[i] = (short)(int)(result * atanOutput);
        }
    }
}
