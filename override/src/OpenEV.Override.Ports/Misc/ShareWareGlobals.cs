using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Misc;

// Shareware-registration state — MANAGED (mainmenu 4-rules B4). The Mac kept a
// pointer cluster at 0x10081254..0x10081268 (4-byte char*/record* slots, reached
// both absolutely and GameToc-relative: -0x740c/-0x7408/-0x626a) plus the
// "date applied" flag byte at 0x100823f6. The port never allocated those pointers, so
// the old port used the slots (inconsistently) as direct buffers/bytes. The
// managed fields below replace the whole cluster.
//
// The session OPENS on a stock EVO install: STR# 900 items 1/4 exist (owner/code
// = "EV Override") and the File Manager substrate (MacToolbox.HfsDataFork) lets
// LoadOrInitPilotPrefsRecord load-or-init the 0x11c record, so
// InitShareWareRegistrationSession sets Registered=1. CheckShareWareRegistrationMatch then
// reports "no match" (no id-900 'REG' record exists → no code match), so TitleMainLoop shows
// the shareware nag (ShowSharewareNagDialog) on every title visit — faithful to an
// unregistered shareware copy. (The id-900 'REG' registration record never materialises, so
// a true "registered" match is unreachable — same as the real unregistered build.)
public static class ShareWareGlobals
{
    public const int RecordPtrSlot = 0x10081254; // use Record
    public const int DateRecordSlot = 0x10081258; // use SessionStartSeconds
    public const int RegCodeBufferSlot = 0x1008125c; // use RegCode
    public const int OwnerNameBufferSlot = 0x10081260; // use OwnerName
    public const int RegisteredFlagSlot = 0x10081268; // use Registered
    public const int RegDateAppliedSlot = 0x100823f6; // use RegDateApplied

    /// Registration-session-open flag (was the byte behind *0x10081268).
    public static byte Registered;
    /// Registered owner name, STR# 900 item 1 (was the Str255 behind *0x10081260).
    public static string OwnerName = "";
    /// Registration code string, STR# 900 item 4 (was the Str255 behind *0x1008125c).
    public static string RegCode = "";
    /// "Apply the session to the install date" flag (was byte 0x100823f6).
    public static byte RegDateApplied;
    /// Session-start timestamp, GetDateTime seconds (was the int behind *0x10081258).
    public static int SessionStartSeconds;
    /// The 0x11c-byte registration/stats record (was the block behind *0x10081254).
    public static readonly RegistrationRecord Record = new();

    /// "A notification is pending" flag (was the data-seg byte 0x100823f7). No ported
    /// writer exists anywhere in the binary, so this faithfully stays 0 forever — kept
    /// for CloseShareWareRegistrationSession's read-site parity.
    public static byte NotificationPending;
}

// The Ambrosia registration/stats record (0x11c bytes, round-tripped through the
// "EV Override Pilots" prefs file by LoadOrInitPilotPrefsRecord /
// WritePilotRecordToPrefsFile as raw bytes). Layout from its readers:
//   +0x000 Str255 — owner name (the EqualString scan key in the prefs file)
//   +0x100 int    — install date (GetDateTime seconds; GetDaysSinceInstall)
//   +0x104 int    — total play seconds (Close adds each session; GetInstallHours /3600)
//   +0x108 int    — registration code word (GetRegistrationCode reads it;
//                   Close INCREMENTS it per counted session — faithful oddity)
//   +0x10c..+0x118 ints — zero-initialised, no reader identified.
public sealed class RegistrationRecord
{
    public const int Size = 0x11c;
    public readonly PilotBlock Block = new(Size);

    public string OwnerName
    {
        get => Block.PascalAt(0x000);
        set => Block.SetPascal(0x000, value, 0x100);
    }
    public int InstallDateSeconds
    {
        get => Block.IntAt(0x100);
        set => Block.SetInt(0x100, value);
    }
    public int TotalPlaySeconds
    {
        get => Block.IntAt(0x104);
        set => Block.SetInt(0x104, value);
    }
    public int RegCodeWord
    {
        get => Block.IntAt(0x108);
        set => Block.SetInt(0x108, value);
    }

    /// The init path zeroes the six counter ints past the install date.
    public void ClearCounters()
    {
        for (int off = 0x104; off <= 0x118; off += 4) Block.SetInt(off, 0);
    }
}
