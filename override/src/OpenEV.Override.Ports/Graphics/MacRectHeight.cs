namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100592b4 (EV Override-11.c lines 36659-36671).
// The "+0xc/+0x10" reads are the bounds rect EMBEDDED in a sprite FRAME record — every
// caller passes a frame handle (frame-pointer table stores / node SpritePtr / spriteRec),
// audited 2026-06-11; none passes a bare Rect address. Managed SpriteFrame fields now.
public static class MacRectHeight
{
    public static int Run(int rectPtr)
    {
        if (rectPtr == 0)
        {
            return 0;
        }
        var f = Model.SpriteFrames.At(rectPtr);
        return f.BoundsBottom - f.BoundsTop;
    }
}
