namespace OpenEV.Override.Ports.Title.Model;

// The slideshow / Notification Manager record — was the heap record behind ptr
// cell 0x100819d0: an NMRec embedded at
// +4 (+8 = its posted short), the slideshow window/teardown fields at
// +0x12d..+0x16a, and a first int (+0) that POINTED at an open-window counter
// (collapsed to a managed int here; reconnect it to that counter if the
// opener is ever ported). The opener (the shareware slideshow) is unported,
// so everything stays zeroed — the same observable as the unseeded BSS
// record the teardown walked.
public static class SlideShowState
{
    public static int OpenWindowCount; // +0  — was a pointer to a counter (collapsed; see class comment)
    public static short NmPosted;        // +8  — NMRec posted flag
    public static byte OpenFlag;        // +0x12d
    public static int Window;          // +0x12e
    public static int HandleA;         // +0x132
    public static int HandleB;         // +0x136
    public static int SndChannel;      // +0x15e
    public static int RoutineDescA;    // +0x162
    public static int CallbackArg;     // +0x166
    public static int RoutineDescB;    // +0x16a
}
