using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1005bca0 (EV Override-11.c lines 37908-37950): centre a PICT-sized
// rect inside the rect at `rectPtr`. The Mac reads the picture's frame from the
// Picture handle (*handle+2 = top/left, +6 = bottom/right) and only ever uses its
// SIZE (bottom-top, right-left); the port's GetPicture returns the PICT id (not a
// real Mac Picture handle), so we take the size from the host PICT texture instead —
// equivalent, and avoids the garbage-deref that the old no-op shim guarded against.
//
// Rect is {top,left,bottom,right} as four shorts. The decompile's `>>1 +
// negative-odd fixup` is signed round-toward-zero division — C# int `/ 2` exactly.
public static class RectCenter
{
    public static void Run(int pictureHandle, short[] rect)
    {
        if (pictureHandle == 0)
            return;
        var tex = MacToolbox.PictResolver?.Invoke(pictureHandle);
        if (tex is null)
            return;
        int picW = tex.Width;
        int picH = tex.Height;
        int deltaV = (rect[2] - rect[0]) - picH;
        short newTop = (short)(rect[0] + deltaV / 2);
        int deltaH = (rect[3] - rect[1]) - picW;
        short newLeft = (short)(rect[1] + deltaH / 2);
        rect[0] = newTop;
        rect[2] = (short)(newTop + picH);
        rect[1] = newLeft;
        rect[3] = (short)(newLeft + picW);
    }
}
