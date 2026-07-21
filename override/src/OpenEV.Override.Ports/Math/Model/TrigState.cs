namespace OpenEV.Override.Ports.EvoMath.Model;

// Managed trig lookup-table subsystem — migrated from the EvoMemory data-segment
// heap/pointer backing (the base-pointer slots 0x100811e8/e4/e0/dc and the result
// pointer 0x100811d8 are now dead).
//
// InitTrigTables fills these once at boot (GameBootSequence step 4); EvMath.Sin360/
// Cos360 look them up (returning the value directly — the old shared result slot
// *_DAT_100811d8 is gone), and Atan2Lookup reads the atan table. All storage is
// plain managed C#; behaviour is unchanged (same values).
public static class TrigState
{
    public const int TableSize = 360;   // 360 degrees

    public static readonly float[] SinTable = new float[TableSize];
    public static readonly float[] CosTable = new float[TableSize];
    public static readonly float[] TanTable = new float[TableSize];
    public static readonly short[] AtanTable = new short[0x400];

    // Table reads.
    public static float Sin(int degree) => SinTable[degree];   // degree 0..359
    public static float Cos(int degree) => CosTable[degree];   // degree 0..359
    public static short Atan(int index) => AtanTable[index];   // index 0..0x3ff
}
