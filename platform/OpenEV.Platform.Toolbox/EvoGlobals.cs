namespace OpenEV.Platform.Toolbox;

// Typed, field-backed facade for Mac global flags/values that were previously
// raw EvoMemory byte/word slots addressed by a hex literal scattered across the
// ports. Each value now lives in EXACTLY ONE place — a real C# field here.
public static class EvoGlobals
{
    // DAT_10082430 — sound-subsystem-booted flag (byte BSS 0x10082430, toc-0x6230 /
    // ppu -0x188c). The sound mixer/volume gate — set by BootSoundSubsystem, cleared
    // by TeardownSoundSubsystem.
    public static bool IsSoundSubsystemBooted;

    // 0x10080f60 → quit flag byte: the main-loop / host exit gate.
    public static bool QuitRequested;   // managed (was the byte behind ptr cell 0x10080f60)

    // 0x10080f80 → player-death flag byte: set on death, cleared on entering a ship.
    public static bool PlayerDead;      // managed (was the byte behind ptr cell 0x10080f80)

    // Set true once Enter Ship has wired the game window. Gates the host's offscreen-game
    // routing + per-frame flush (RenderFrame drains into the game GWorld rather than the
    // virtual target) so it never disturbs the title screen.
    public static volatile bool GameWorldActive;

    // DAT_1008f728 / DAT_1008f72a — active explosion- and projectile-streak sprite
    // counters: separate SHORT cells 2 bytes apart. Keep them as separate shorts — an
    // int-width read/write spans f728..f72b and overlaps the streak counter at f72a.
    // Values stay in [0,0x40]; exposed as int.
    private static short _activeExplosionCount;
    private static short _activeStreakCount;

    public static int ActiveExplosionCount
    { get => _activeExplosionCount; set => _activeExplosionCount = (short)value; }

    public static int ActiveStreakCount
    { get => _activeStreakCount; set => _activeStreakCount = (short)value; }

    // 0x1008f732 (GameToc+0x70d2 — decompile renders every access as raw `local + 0x70d2`
    // offset arithmetic, no DAT_ symbol) — the resource-file refNum CurResFile() returned
    // at boot, captured once by InitPrefsPathAndBugBits and reused downstream as the
    // search-context refNum for ResolveMacFileAlias, OpenPluginResourceFiles' EV Plug-Ins
    // folder scan, and the deferred QuickTime movie-folder lookup (PlayQuickTimeMovie).
    // Never rewritten after boot. Distinct from PluginResourceRefs slot 1 (0x100870d2).
    public static short BootResFileRefNum;

    // UNK_10084e6c — shareware user-mode (ushort in the decompile: 1=normal, 2=admin).
    // Keep it a ushort (matches `ushort UNK_10084e6c`): a byte-width write only round-trips
    // while the upper 3 bytes are 0. Exposed as int.
    private static ushort _shareWareUserMode;
    public static int ShareWareUserMode
    { get => _shareWareUserMode; set => _shareWareUserMode = (ushort)value; }

    public static void Reset()
    {
        IsSoundSubsystemBooted = false;
        _activeExplosionCount = 0;
        _activeStreakCount = 0;
        _shareWareUserMode = 0;
    }
}
