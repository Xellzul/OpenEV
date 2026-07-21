using System;

namespace OpenEV.Platform.Toolbox;

// Classic Mac Event Manager mask bits (Inside Macintosh: Toolbox Essentials) — the
// FlushEvents/GetNextEvent/EventAvail "eventMask" parameter type. Each named bit is
// `1 << MacEventType.X`, matching Apple's own xxxMask constants.
[Flags]
public enum EventMask : short
{
    // 1 << MacEventType.NullEvent. NOT a real Apple mask — nullEvent (code 0) is
    // synthesized when the queue is empty, never queued, so no official mask bit
    // exists for it. Present here only because several decompiled FlushEvents calls
    // pass this bit — see the OGB note on MacToolbox.FlushEvents.
    NullEventMask = 0x0001,
    MouseDownMask = 0x0002, // 1 << MacEventType.MouseDown   (Apple: mDownMask)
    MouseUpMask   = 0x0004, // 1 << MacEventType.MouseUp     (Apple: mUpMask)
    KeyDownMask   = 0x0008, // 1 << MacEventType.KeyDown     (Apple: keyDownMask)
    KeyUpMask     = 0x0010, // 1 << MacEventType.KeyUp       (Apple: keyUpMask)
    AutoKeyMask   = 0x0020, // 1 << MacEventType.AutoKey     (Apple: autoKeyMask)
    UpdateMask    = 0x0040, // 1 << MacEventType.UpdateEvt   (Apple: updateMask)
    DiskMask      = 0x0080, // 1 << MacEventType.DiskEvt     (Apple: diskMask)
    ActivateMask  = 0x0100, // 1 << MacEventType.ActivateEvt (Apple: activMask)
    OsMask        = unchecked((short)0x8000), // 1 << MacEventType.OsEvt (Apple: osMask)
    EveryEvent    = unchecked((short)0xFFFF), // Apple: everyEvent (all events)
}
