namespace OpenEV.Platform.Toolbox;

// Classic Mac ADB virtual keycodes (Inside Macintosh, US keyboard layout). These
// are the values stored in the EVO key-binding map (Misc.ActiveKeyMap) and tested
// by the keymap-bit helpers; XnaKeyToMacKeycode maps MonoGame Keys onto them.
// Underlying int so None (-1, "no Mac key for this XNA key") fits.
public enum MacKeycode
{
    None = -1,

    // Letters
    A = 0x00, S = 0x01, D = 0x02, F = 0x03, H = 0x04, G = 0x05, Z = 0x06, X = 0x07,
    C = 0x08, V = 0x09, Section = 0x0A, B = 0x0B, Q = 0x0C, W = 0x0D, E = 0x0E, R = 0x0F, Y = 0x10,
    T = 0x11, O = 0x1F, U = 0x20, I = 0x22, P = 0x23, L = 0x25, J = 0x26, K = 0x28,
    N = 0x2D, M = 0x2E,

    // Number row
    D1 = 0x12, D2 = 0x13, D3 = 0x14, D4 = 0x15, D6 = 0x16, D5 = 0x17,
    D9 = 0x19, D7 = 0x1A, D8 = 0x1C, D0 = 0x1D,

    // Punctuation
    Equal = 0x18, Minus = 0x1B, RightBracket = 0x1E, LeftBracket = 0x21,
    Quote = 0x27, Semicolon = 0x29, Backslash = 0x2A, Comma = 0x2B, Slash = 0x2C,
    Period = 0x2F, Grave = 0x32,

    // Whitespace / editing
    Tab = 0x30, Space = 0x31, Delete = 0x33, ForwardDelete = 0x75, Return = 0x24, Escape = 0x35,

    // Modifiers
    Command = 0x37, Shift = 0x38, CapsLock = 0x39, Option = 0x3A, Control = 0x3B,
    RightShift = 0x3C, RightOption = 0x3D,

    // Keypad / extended (well-known ADB codes; the 0xNN-named ones are uncommon
    // keys the EVO defaults reference but that have no standard symbolic name).
    KeypadMultiply = 0x43, Keypad7 = 0x59, KeypadDivide = 0x4B,
    Key0x46 = 0x46, Key0x4D = 0x4D, Key0x70 = 0x70,

    // Function keys
    F1 = 0x7A, F2 = 0x78, F3 = 0x63, F4 = 0x76, F12 = 0x6F, F14 = 0x6B, F15 = 0x71,

    // Navigation cluster
    Help = 0x72, Home = 0x73, PageUp = 0x74, End = 0x77, PageDown = 0x79, Power = 0x7F,

    // Arrows
    LeftArrow = 0x7B, RightArrow = 0x7C, DownArrow = 0x7D, UpArrow = 0x7E,
}
