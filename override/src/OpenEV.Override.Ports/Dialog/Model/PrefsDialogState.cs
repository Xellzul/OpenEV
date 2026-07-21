using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Dialog.Model;

// Managed home for the Set Prefs dialog family: the Set Prefs dialog itself
// (DLOG 0xfa1, FUN_10044480), the Game Speed sub-dialog (DLOG 0xfa2,
// FUN_100451f0), the keybind-grid redraw (FUN_10044ef4), the key-capture
// modal filter (FUN_10044d68) and the game-speed slider redraw
// (FUN_10045504).
//
// Replaces the PrefsMemory pointer-cell wiring and its 0x1020xxxx scratch
// backing; see each field's own comment for its specific cell.
public static class PrefsDialogState
{
    // ── Set Prefs dialog (DLOG 0xfa1) ─────────────────────────────────
    public static int DialogWindow;            // DialogPtr (ex *0x100810cc → cell 0x1020205c)
    public static short SelectedKeybindSlot;   // armed keybind slot 0..0x1e (ex *0x100810dc → 0x10202058)
    public static readonly int[] VolumePicts = new int[5];  // PICTs 0x86..0x8a: vol-up/down normal+pressed, sound-volume art (ex *0x100810e4 → 0x10202410)
    public static int Pict132Handle;           // PICT 0x84 "Keys" grid backdrop (ex **(toc-0x7590) → 0x10202060)

    // Sound Volume readout labels — STR# 136 (0x88), one per level 0..8
    // ("Off" .. "Friggin' Loud"). Ex the 9×0x100 Str255 table the old
    // PrefsMemory staged at 0x10203000 (*(toc-0x7834)). Loaded by
    // PrefsMemory.Init.
    public static readonly string[] VolumeLabels = new string[9];

    // The key-capture modal filter proc (FUN_10044d68). Its Mac code address
    // doubles as the registry sentinel — ex *0x100810e0.
    public const int KeyAssignFilterProc = 0x10044d68;

    // ── Game Speed sub-dialog (DLOG 0xfa2) ────────────────────────────
    public static int GameSpeedDialogWindow;   // DialogPtr (ex *0x100810c4 → 0x10202430)
    public static readonly int[] GameSpeedPicts = new int[2];  // PICT 400 track / 401 thumb (ex *0x100810c8 → 0x10202438)

    // Slider position 0..0xaf; displayed/saved percent = value + 50
    // (ex **(toc-0x758c) == *0x100810d4 → cell 0x10202064).
    public static short GameSpeedPercent;

    // The live game-speed double (1.0 = 100%), ex **(double**)(toc-0x785c).
    // toc(GameToc 0x10088660) - 0x785c = 0x10080e04 = the pointer cell whose
    // target 0x100e0200 IS the physics time-scale: the SAME cell as
    // WorldState.CpuSpeedScale (the benchmark seeds it on the no-prefs boot, the
    // saved/dialog game-speed pref overrides it, and CopyCpuSpeedScaleToTimeScale
    // copies it into TimeScale every frame) — so this setting actually drives
    // ship motion. The loader (FUN_10019f88), the writer (FUN_1001a3b8) and the
    // Game Speed dialog slider (FUN_10044480) all read/write this one cell in
    // the original.
    public static double GameSpeed
    {
        get => WorldState.CpuSpeedScale;
        set => WorldState.CpuSpeedScale = value;
    }
}

// Game-speed scale constants, extracted from the original PEF data segment
// (real RTOC 0x10088660; see game/tools/pefdump). The prefs/dialog code read
// them as TOC-relative doubles; under the port's unseeded title TOC they were
// installed by PrefsMemory.Init — now C# literals (data-seg→literal rule).
// Game speed is a percent: speed = percent/100, 100 = normal, saved 50..225.
public static class GameSpeedScale
{
    public const double SaveScale = 100.0;  // A  (toc-0x6930): saved short = (int)(A * speed)
    public const double LoadScale = 0.01;   // B  (toc-0x6958): speed = saved * B
    public const double SliderScale = 100.0;  // C1 (toc-0x65a8): slider = (int)(C1 * speed + C2)
    public const double SliderBias = -50.0;  // C2 (toc-0x65a0): speed = (slider - C2) / C1
    // The i2d 2^52 bias doubles at toc-0x6598/-0x6950 are PpcMagic.I2dBias.
}
