using System.Diagnostics;

namespace OpenEV.Platform.Toolbox;

// Classic Mac TickCount() — 60 ticks per second since boot. EVO uses ticks for
// animation timing, mouse-up delays, "Are you sure you want to quit" cancellation, etc.
public static class TickCount
{
    private static readonly Stopwatch _sw = Stopwatch.StartNew();
    public static uint Get() => (uint)(_sw.Elapsed.TotalSeconds * 60.0);
}
