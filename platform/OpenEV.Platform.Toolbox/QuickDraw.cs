namespace OpenEV.Platform.Toolbox;

// Classic Mac Point: vertical first, then horizontal. Mutable to match the
// Pascal/C semantics the decompile relies on.
public struct MPoint
{
    public short V, H;
    public MPoint(short v, short h) { V = v; H = h; }
    public override string ToString() => $"({V},{H})";
}
