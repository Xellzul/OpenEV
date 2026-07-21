namespace OpenEV.Override.Ports.Title.Model;

// MANAGED title sound-channel handle table.
//
// The Mac global at 0x100810f8 (`_DAT_100810f8`, ≡ *(toc-0x7568)) held a
// POINTER to a 4-entry array of sound-channel handles; the decompile always
// dereferences the slot, then indexes: *(int *)(_DAT_100810f8 + channel * 4).
// The port holds the four handles directly.
//
// Every known caller (DispatchTitleEvent, AnimateRowReveal, TitleMainLoop,
// RunGameSessionLauncher — the only functions that touch off_810F8 in the
// disasm) only ever indexes channels 1-3: 1 = button-click chime, 2/3 =
// row-reveal chimes. Channel 0 is never allocated, indexed, or disposed by any
// of them (the teardown loop runs channels 1..3, not 0); the title's background
// music is a separate mechanism (Sound.Model.SoundFilePlayState.FileMusicChannel,
// cell 0x10081088). Slot 0 is a genuinely unused array slot — NOT a music
// channel; kept only to preserve the 4-entry array shape.
public static class SndChannelTable
{
    public const int PtrSlot = 0x100810f8;   // _DAT_100810f8: former ptr-to-array cell (see header)
    public const int Count = 4;

    private static readonly int[] _handles = new int[Count];

    // get/set the sound-channel handle in the given slot (0..3).
    public static int Handle(int channel) => _handles[channel];
    public static void SetHandle(int channel, int handle) => _handles[channel] = handle;
}
