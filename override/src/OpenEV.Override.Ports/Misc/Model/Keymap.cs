using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc.Model;

// The keyboard / key-binding subsystem, consolidated and managed:
//   * Active key-binding map — the 45 game-control slots (managed short[]).
//   * Live keymap-copy buffer — what the prefs "Configure Controls" dialog edits.
//   * Pack/Unpack between the two; the prefs conflict check.
//   * The cached + live hardware KeyMap snapshots and their bit tests.
//   * The default bindings seeded at boot.
//
// Ports: FUN_100548b8 (InitDefaultMacKeyBindings), FUN_1005bfc8 (PackKeyBindings),
// FUN_1005c0dc (UnpackKeyBindings), FUN_1005c1f0 (KeybindConflictCheck),
// FUN_1005f900/928/964 (RefreshCachedKeymap / TestCachedKeymapBit / TestLiveKeymapBit),
// FUN_1005beb4 (PollFirstHeldUserKey).
public static class Keymap
{
    // ── Active key-binding map ───────────────────────────────────────────
    // A FIXED BSS short[45] at 0x1008a558 in the Mac binary; its data now lives entirely in
    // the managed Store. Each slot holds a Mac ADB keycode bound to one control; the
    // slot→action label is the prefs DITL 4001 item order (item N ⇒ slot N-3).
    // Slots are SHORT — reading one as a byte yields the BE high byte (the keycode < 0x100
    // bug); use Slot()/SetSlot().
    public const int SlotCount = 45;
    public static readonly short[] Store = new short[SlotCount];

    public static MacKeycode Slot(KeyAction slot) => (MacKeycode)Store[(int)slot];
    public static void SetSlot(KeyAction slot, int keycode) => Store[(int)slot] = (short)keycode;

    // ── Live keymap-copy buffer (prefs "Configure Controls" edits) ───────
    // A managed short[36], owned here (was the shared EvoMemory heap block at
    // *LiveKeymapCopySlot, _DAT_10080e08). The prefs dialog, the defaults, and prefs
    // save/load all read/write it via LiveGet/LiveSet. PackKeyBindings fills the first
    // 31 (LiveCount); 31..35 are extra keymap shorts the prefs blob round-trips.
    // WritePrefsToDisk serializes all 36 to disk (the on-disk layout is unchanged by
    // the move).
    public const int LiveSize = 36;
    internal const int LiveCount = 31;
    private static readonly short[] LiveStore = new short[LiveSize];

    public static short LiveGet(int index) => LiveStore[index];
    public static void LiveSet(int index, short value) => LiveStore[index] = value;

    // Live-buffer index -> the ActiveKeyMap action it mirrors, in prefs DITL 4001 item
    // order. Pack and Unpack share this exact mapping.
    private static readonly KeyAction[] LiveOrder =
    {
        KeyAction.Action24, KeyAction.Action25, KeyAction.TurnRight, KeyAction.TurnLeft, KeyAction.Action27,
        KeyAction.Action7,  KeyAction.Action12, KeyAction.Action13,  KeyAction.Action14, KeyAction.Action8,
        KeyAction.Action17, KeyAction.Action18, KeyAction.Action19,  KeyAction.FirePrimary,  KeyAction.Action3,
        KeyAction.Action0,  KeyAction.Action1,  KeyAction.Action10,  KeyAction.Action11, KeyAction.Action26,
        KeyAction.Action6,  KeyAction.Action4,  KeyAction.Land,   KeyAction.Action15, KeyAction.Action16,
        KeyAction.Action20, KeyAction.Action21, KeyAction.Action9,   KeyAction.Action28, KeyAction.Action43,
        KeyAction.Action44,
    };

    // FUN_1005bfc8 — copy the active keymap into the live edit buffer (dialog order).
    public static void PackKeyBindings()
    {
        for (int i = 0; i < LiveCount; i++)
            LiveSet(i, (short)Slot(LiveOrder[i]));
    }

    // FUN_1005c0dc — copy the live edit buffer back into the active keymap.
    public static void UnpackKeyBindings()
    {
        for (int i = 0; i < LiveCount; i++)
            SetSlot(LiveOrder[i], LiveGet(i));
    }

    // FUN_1005c1f0 — prefs Save conflict check: is `keyCode` already bound to a live
    // slot other than `selfSlot`? The Mac split the scan into two passes with skip-lists:
    // pass 1 scans dialog-order indices 0..30, skipping indices 23/25/26; pass 2 re-scans
    // just 23..26, skipping 24. Those four indices are a block of adjacent prefs DITL 4001
    // items (LiveOrder[23..26] = Action15/Action16/Action20/Action21) but no further
    // real-world grouping is documented; preserved verbatim from the decompile.
    public static bool KeybindConflictCheck(short keyCode, short selfSlot)
    {
        for (short slot = 0; slot < LiveCount; slot = (short)(slot + 1))
            if (keyCode == LiveGet(slot) && slot != selfSlot && slot != 23 && slot != 25 && slot != 26)
                return true;
        for (short slot = 23; slot < 27; slot = (short)(slot + 1))
            if (keyCode == LiveGet(slot) && slot != selfSlot && slot != 24)
                return true;
        return false;
    }

    // ── Hardware KeyMap snapshots + bit tests ────────────────────────────
    // The cached KeyMap (was the BSS buffer at 0x100811f0): a managed ushort[16] filled by
    // GetKeys (first 8 words = the 128-bit Mac KeyMap, rest 0 so an out-of-range key reads
    // "not pressed"). RefreshCachedKeymap snapshots it; the per-frame input layer tests it.
    private static readonly ushort[] _cachedKeymap = new ushort[16];

    // Is keycode `keyCode` set in a 128-bit Mac KeyMap (word (keyCode>>4)&0xf, bit
    // keyCode&0xf)? Returns the masked bit (non-zero = held). Shared by the tests below:
    // FUN_1005f928 reads the cached snapshot; FUN_1005f964/beb4 pass a fresh GetKeys buffer.
    private static int KeyMapBit(ushort[] keyMap, int keyCode)
        => (1 << (keyCode & 0xf)) & keyMap[(keyCode >> 4) & 0xf];

    // FUN_1005f900 — snapshot the hardware KeyMap into the cache.
    public static void RefreshCachedKeymap() => MacToolbox.GetKeys(_cachedKeymap);

    // FUN_1005f928 — is `keyCode` held in the cached snapshot?
    public static int TestCachedKeymapBit(int keyCode) => KeyMapBit(_cachedKeymap, keyCode);

    // DEVIATION (faithful): overload taking a real MacKeycode, applying the same ^8 keymap-
    // space translation as Keymap.TestLiveKeymapBit(int)'s keycode-space note describes, so
    // callers can pass a named key instead of the raw XOR-8 bit index.
    public static int TestCachedKeymapBit(MacKeycode keyCode) => KeyMapBit(_cachedKeymap, (int)keyCode ^ 0x08);

    // Keycode space (memory reference_evo_keycode_space has the full derivation): the ^8 is a
    // real artifact of the ORIGINAL PowerPC binary's GetKeys() layout, not a v2-host invention —
    // proven via STR#129 vs FUN_100548b8. A decompile-literal argument (e.g. 0x32 — whose
    // PHYSICAL key is 0x32^8 = 0x3A = Option, NOT Grave; the raw literal reads as Grave only
    // if you forget the ^8) is ALREADY real-ADB-keycode XOR 8 ("EVO/decompile-literal space").
    //
    // A value that's ALREADY in EVO/decompile-literal space — an ActiveKeyMap slot (Keymap.Slot,
    // cast to int) or a PollFirstHeldUserKey result — must hit this raw `int` overload with NO
    // further transform: FUN_1005f964 itself passes such values straight through (its Set-Prefs-
    // dialog filter-proc caller and TickShipAI's ship-class-cycle debug hotkeys both pass their
    // PollFirstHeldUserKey/Slot value verbatim). A REAL ADB key CONSTANT (MacKeycode.Grave,
    // .Option, ...) instead needs the `MacKeycode` overload below, which applies the ^8
    // translation INTO EVO space. Mixing the two up breaks a "wait for physical release" loop
    // on its very first spin, or silently tests the wrong key.
    public static int TestLiveKeymapBit(int keyCode)
    {
        ushort[] km = new ushort[16];
        MacToolbox.GetKeys(km);
        return KeyMapBit(km, keyCode);
    }

    // FUN_1005f964 — same bit test against a fresh KeyMap read (not the cache), for a REAL ADB
    // key CONSTANT (see the keycode-space note above for the `int` overload).
    public static int TestLiveKeymapBit(MacKeycode keyCode)
    {
        ushort[] km = new ushort[16];
        MacToolbox.GetKeys(km);
        return KeyMapBit(km, (int)keyCode ^ 0x08);
    }

    // Keycodes excluded from user binding (modifiers / reserved / arrow-flight group).
    private static bool IsReservedKeycode(int keyCode) =>
        keyCode is 0x1a or 0x1b or 0x1c or 0x1d or 0x77 or 0x31 or 0x72 or 0x70 or 0x7e or 0x6b or 0x68;

    // FUN_1005beb4 — the lowest-numbered currently-held key (0..127) that is a valid
    // user-bindable key, skipping the reserved keycodes; 0xffffffff if none held.
    public static uint PollFirstHeldUserKey()
    {
        ushort[] km = new ushort[14];
        MacToolbox.GetKeys(km);
        for (uint keyCode = 0; keyCode <= 127; keyCode++)
            if (!IsReservedKeycode((int)keyCode) && KeyMapBit(km, (int)keyCode) != 0)
                return keyCode;
        return 0xffffffff;
    }

    // ── Defaults ─────────────────────────────────────────────────────────
    // FUN_100548b8 — seed the default control bindings, by action-slot 0..44. Each value
    // is a Mac ADB virtual keycode (inline comment = the physical key). The original
    // emitted three out of order (slots 15/20/21); each slot is written once with no read
    // between, so index order is identical. Flight defaults 22-25 are EVO's arrow keys
    // (the numeric 0x73/74/76/75 become Left/Right/Up/Down after host modern->EVO ^0x08).
    public static void InitDefaultMacKeyBindings()
    {
        for (int slot = 0; slot < DefaultBindings.Length; slot++)
            SetSlot((KeyAction)slot, (int)DefaultBindings[slot]);
    }

    private static readonly MacKeycode[] DefaultBindings =
    {
        MacKeycode.G,            // 0
        MacKeycode.V,            // 1
        MacKeycode.CapsLock,     // 2
        MacKeycode.Delete,       // 3  (Backspace)
        MacKeycode.Equal,        // 4
        MacKeycode.N,            // 5
        MacKeycode.Slash,        // 6
        MacKeycode.C,            // 7
        MacKeycode.Option,       // 8
        MacKeycode.J,            // 9
        MacKeycode.Shift,        // 10
        MacKeycode.X,            // 11
        MacKeycode.Q,            // 12
        MacKeycode.I,            // 13
        MacKeycode.M,            // 14
        MacKeycode.U,            // 15
        MacKeycode.F,            // 16
        MacKeycode.B,            // 17
        MacKeycode.A,            // 18
        MacKeycode.S,            // 19
        MacKeycode.Z,            // 20
        MacKeycode.Section,      // 21  § / ISO key
        MacKeycode.Home,         // 22  turn CCW  (0x73 = EVO Left arrow)
        MacKeycode.PageUp,       // 23  turn CW   (0x74 = EVO Right arrow)
        MacKeycode.F4,           // 24  thrust    (0x76 = EVO Up arrow)
        MacKeycode.ForwardDelete,// 25  reverse   (0x75 = EVO Down arrow)
        MacKeycode.RightOption,  // 26
        MacKeycode.E,            // 27
        MacKeycode.Comma,        // 28
        MacKeycode.Help,         // 29  Help / Insert
        MacKeycode.Key0x70,      // 30  (uncommon/extended)
        MacKeycode.F14,          // 31
        MacKeycode.UpArrow,      // 32  thrust/accelerate
        MacKeycode.F1,           // 33
        MacKeycode.DownArrow,    // 34  decelerate/reverse
        MacKeycode.LeftArrow,    // 35  turn CCW
        MacKeycode.RightArrow,   // 36  turn CW
        MacKeycode.F15,          // 37
        MacKeycode.KeypadDivide, // 38
        MacKeycode.D0,           // 39  0 (zero)
        MacKeycode.Power,        // 40
        MacKeycode.Key0x4D,      // 41  (uncommon/extended keypad)
        MacKeycode.Key0x46,      // 42  (uncommon/extended keypad)
        MacKeycode.Backslash,    // 43
        MacKeycode.K,            // 44
    };
}
