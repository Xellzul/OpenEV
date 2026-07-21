namespace OpenEV.Platform.Toolbox;

// Classic Mac EventRecord 'what' codes (Inside Macintosh: Toolbox Essentials) — the
// value WaitNextEvent/GetNextEvent returns in EventRecord.what and that the managed
// MacEvent.What carries. Cast the raw ushort/short at the toolbox boundary
// ((MacEventType)eventCode), the same way MacKeycode is used for keycodes.
public enum MacEventType : ushort
{
    NullEvent      = 0,
    MouseDown      = 1,
    MouseUp        = 2,
    KeyDown        = 3,
    KeyUp          = 4,
    AutoKey        = 5,
    UpdateEvt      = 6,
    DiskEvt        = 7,
    ActivateEvt    = 8,
    OsEvt          = 15,
    HighLevelEvent = 23,
}
