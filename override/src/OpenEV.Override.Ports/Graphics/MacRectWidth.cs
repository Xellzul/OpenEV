namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10059294 (EV Override-11.c lines 36648-36658).
// The "+0xe/+0x12" reads are the bounds rect EMBEDDED in a sprite FRAME record — every
// caller passes a frame handle (frame-pointer table stores / node SpritePtr / spriteRec),
// audited 2026-06-11; none passes a bare Rect address. Managed SpriteFrame fields now.
public static class MacRectWidth
{
    public static int Run(int rectPtr)
    {
        if (rectPtr == 0)
        {
            return 0;
        }
        var f = Model.SpriteFrames.At(rectPtr);
        return f.BoundsRight - f.BoundsLeft;
    }
}
