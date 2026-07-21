namespace OpenEV.Override.Ports.Misc.Model;

// Managed home for the player-input one-shot edge-detect latches used by
// Combat/TickShipAI (FUN_10027830). The original kept one byte per key action in a
// contiguous PEF data-segment cluster at 0x10081E44..0x10081E60 (all initially 0),
// addressed two ways in the decompile off the gameplay TOC register ppuVar5
// (= GameToc 0x10088660):
//   *(char *)(ppuVar5 + -0x1a0N)        -> int*-scaled  (offset x4): -0x1a04*4 = 0x10081E50 etc.
//   *(char *)((int)ppuVar5 + -0x680N)   -> raw byte offset:          -0x680f   = 0x10081E51 etc.
// Pattern at every site: key up -> latch = false; key down && !latch -> latch = true
// + perform the action once (sound/dialog/chatter).
public static class PlayerKeyLatches
{
    // 0x10081E44 (toc-0x1a07*4) — MaxMem result shown by the free-memory chatter line.
    public static int FreeMemoryBytes;
    // 0x10081E48 (toc-0x1a06*4) was only the MaxMem grow out-param scratch — no field.

    // 0x10081E4C (toc-0x1a05*4, an int cell) — "within hyper range of a linked system"
    // one-shot: plays UiSoundBankA4 + sets the spawn-pulse dirty flag once on entry.
    public static bool HyperRangeReachedLatch;

    public static bool AutopilotKeyLatch;        // 0x10081E50 (toc-0x1a04*4) — Action8/Action12 (autopilot engage / autopilot-to-history)
    public static bool HyperTargetCycleKeyLatch; // 0x10081E51 (toc-0x680f)   — Action13 (cycle hyper destination among explored links)
    public static bool PlanetSelectKeyLatch;     // 0x10081E52 (toc-0x680e)   — planet-select keys (raw 0x68 / 0x3f+0x1f and number keys)
    // 0x10081E53 (toc-0x680d) — jump-settle gate flag. NEVER set to 1 anywhere (the
    // cluster is TickShipAI-exclusive and only writes 0 / reads != 0) — faithful quirk.
    public static bool JumpSettleFlag;
    public static bool CloakToggleKeyLatch;         // 0x10081E54 — Action44, the cloak toggle key (TickShipAI -> Engage/DisengageCloaking when the player owns a CloakingDevice outfit)
    public static bool EscortCommandKeyLatch;    // 0x10081E55 (toc-0x680b)   — Action17/18/19 (escort/jump-abort command keys)
    public static bool WeaponCycleKeyLatch;      // 0x10081E56 (toc-0x680a)   — Action10 (cycle secondary weapon)
    public static bool CaptureDialogKeyLatch;    // 0x10081E57 (toc-0x6809)   — Action28 (ShowDomainCaptureDialog)
    public static bool MapKeyLatch;              // 0x10081E58 (toc-0x1a02*4) — Action9 (open galaxy map / RunGalaxyMapDialog)
    public static bool JumpKeyLatch;             // 0x10081E59 (toc-0x6807)   — Action14 (initiate hyperspace jump)
    public static bool SecondaryTriggerKeyLatch; // 0x10081E5A (toc-0x6806)   — Action0 (select/arm secondary weapon trigger)
    public static bool LandKeyLatch;             // 0x10081E5B (toc-0x6805)   — Land key
    public static bool BoardKeyLatch;            // 0x10081E5C (toc-0x1a01*4) — Action16 (board disabled target)
    public static bool TargetSpecialKeyLatch;    // 0x10081E5D (toc-0x6803)   — Action33 / raw 0x69 (special target select)
    public static bool TargetNearestKeyLatch;    // 0x10081E5E (toc-0x6802)   — Action11 (target nearest engageable/active ship)
    public static bool OutfitterKeyLatch;        // 0x10081E5F (toc-0x6801)   — Action43 (open outfitter dialog in flight)
    public static bool HailKeyLatch;             // 0x10081E60 (toc-0x1a00*4) — Action4 (hail / PlayerHailAction)
}
