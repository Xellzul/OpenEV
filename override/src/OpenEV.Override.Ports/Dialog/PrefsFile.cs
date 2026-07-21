using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Platform.Toolbox;
namespace OpenEV.Override.Ports.Dialog;

// The on-disk 'Mp¨Ä' id-0x80 prefs blob (0x74 bytes, big-endian) — the format
// WritePrefsToDisk (FUN_1001a3b8) writes and the boot prefs-load
// (FUN_10019f88 / ApplyDefaultPrefsToMemory) reads. Byte-identical to the
// original: every flag byte widens to a big-endian short at these offsets.
//
//   +0x00 version (0x68)        +0x50 game-speed percent (50..225)
//   +0x02 intro music           +0x60 old-OS-warning latched
//   +0x04 pref byte 551         +0x62 QuickTime movies disabled
//   +0x06 master volume (0..8)  +0x64 use QuickDraw
//   +0x08 keymap (36 shorts)    +0x66 projectile streaks disabled
//                               +0x68 six zero shorts (explicit fill)
public static class PrefsFile
{
    public const int BlobSize = 0x74;
    public const short Version = 0x68;

    public const int ResType = 0x4d70a8c4;   // 'Mp¨Ä'
    public const int ResId = 0x80;
    // Creator is the app-wide signature MacFileType.EvoCreator ('EsçO'); kept central
    // there since the pilot file uses the same code.
    public const int FileType = 0x4f70a8c4;   // 'Op¨Ä' — FSpCreateResFile type
    public const string ResourceName = "EV Prefs";   // toc-0x5456 (PEF dump, Pascal 08 "EV Prefs")

    private static short Bool16(int flag) => flag != 0 ? (short)1 : (short)0;

    /// Serialize the live prefs into the on-disk blob — the managed
    /// equivalent of FUN_1001a3b8's NewHandle(0x74) + field pokes.
    public static byte[] BuildBlob()
    {
        var b = new byte[BlobSize];
        BigEndian.WriteInt16(b, 0x00, Version);
        BigEndian.WriteInt16(b, 0x02, Bool16(Core.Model.GamePrefs.IntroMusicEnabled));
        BigEndian.WriteInt16(b, 0x04, Bool16(Core.Model.GamePrefs.PrefByte551));
        BigEndian.WriteInt16(b, 0x62, Bool16(Core.Model.GamePrefs.QuickTimeMoviesDisabled));
        BigEndian.WriteInt16(b, 0x64, Bool16(Core.Model.GamePrefs.UseQuickdraw));
        BigEndian.WriteInt16(b, 0x66, Bool16(Core.Model.GamePrefs.ProjectileStreaksDisabled));
        BigEndian.WriteInt16(b, 0x06, Core.Model.GamePrefs.MasterVolume);
        Misc.Model.Keymap.PackKeyBindings();   // FUN_1005bfc8 — refresh LIVE before saving it
        for (short i = 0; i < Misc.Model.Keymap.LiveSize; i++)
            BigEndian.WriteInt16(b, 8 + i * 2, Misc.Model.Keymap.LiveGet(i));
        // saved percent = (int)(A * speed), A = 100 (ex *(toc-0x6930)).
        BigEndian.WriteInt16(b, 0x50, (short)(int)(GameSpeedScale.SaveScale * PrefsDialogState.GameSpeed));
        // +0x60 = the old-OS-warning latch (ex **(toc-0x7854) = *0x10080e0c).
        BigEndian.WriteInt16(b, 0x60, Core.Model.SystemGlobals.OldOsWarningAcknowledged ? (short)1 : (short)0);
        // +0x68: six explicit zero shorts (the decompile's zero-fill loop —
        // kept as a loop so the written extent stays bug-for-bug obvious).
        for (short i = 0; i < 6; i++)
            BigEndian.WriteInt16(b, 0x68 + i * 2, 0);
        return b;
    }

    /// Apply a version-0x68 blob over the live prefs — the managed equivalent
    /// of FUN_10019f88's version-ok branch. Caller checks the version short.
    public static void ApplyBlob(byte[] blob)
    {
        // QuickTimeMoviesDisabled stores 1 = movies-disabled, matching the
        // dialog's inverted checkbox; the blob keeps that same semantic.
        Core.Model.GamePrefs.IntroMusicEnabled = (byte)Bool16(BigEndian.ReadInt16(blob, 0x02));
        Core.Model.GamePrefs.PrefByte551 = (byte)Bool16(BigEndian.ReadInt16(blob, 0x04));
        Core.Model.GamePrefs.QuickTimeMoviesDisabled = (byte)Bool16(BigEndian.ReadInt16(blob, 0x62));
        Core.Model.GamePrefs.UseQuickdraw = (byte)Bool16(BigEndian.ReadInt16(blob, 0x64));
        Core.Model.GamePrefs.ProjectileStreaksDisabled = (byte)Bool16(BigEndian.ReadInt16(blob, 0x66));
        Core.Model.GamePrefs.MasterVolume = BigEndian.ReadInt16(blob, 0x06);
        Core.Model.SystemGlobals.OldOsWarningAcknowledged = BigEndian.ReadInt16(blob, 0x60) != 0;

        // FUN_100548b8 then the 36 saved shorts → LIVE, then unpack LIVE →
        // PrefsRecord (so the dialog's PackKeyBindings re-derives the same
        // LIVE on open).
        Misc.Model.Keymap.InitDefaultMacKeyBindings();
        for (int i = 0; i < Misc.Model.Keymap.LiveSize; i++)
            Misc.Model.Keymap.LiveSet(i, BigEndian.ReadInt16(blob, 8 + i * 2));
        Misc.Model.Keymap.UnpackKeyBindings();

        // speed = (double)saved * B, B = 0.01 (ex *(toc-0x6958)); the
        // decompile's signed int->double idiom is a plain cast.
        PrefsDialogState.GameSpeed = (double)BigEndian.ReadInt16(blob, 0x50) * GameSpeedScale.LoadScale;
    }
}
