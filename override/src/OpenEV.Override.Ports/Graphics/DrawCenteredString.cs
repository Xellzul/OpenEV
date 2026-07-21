using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1005920c (EV Override-11.c lines 36623-36647).
public static class DrawCenteredString
{
    // Centre a C# string between leftEdge..rightEdge at baselineY. Measures/draws the
    // string directly — no Mac-memory read. (The original FUN_1005920c's >>1 +
    // round-toward-zero bit-twiddle is exactly integer /2.)
    public static void Run(string s, short leftEdge, short rightEdge, short baselineY)
    {
        int centerX = (leftEdge + rightEdge) / 2 - MacToolbox.StringWidth(s) / 2;
        MacToolbox.MoveTo(centerX, baselineY);
        MacToolbox.DrawString(s);
    }
}
