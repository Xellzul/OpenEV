using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics.Model;

// Graphics/render globals — migrated to MANAGED memory. The scalar flags/counters
// and pointer-valued cells below are now managed static fields; the raw BSS cells
// (the *Slot consts) are kept only as address documentation, EvoMemory itself
// being gone. Pointer fields hold the Mac pointer/handle value; the pointed-to
// heap structure lived in EvoMemory on the original Mac binary.
//
// NOT migrated (a raw address constant, by nature — never read through EvoMemory
// in the port):
//   • PilotInfoLabelStr (0x10082d47): an &DAT address into the PEF data segment.
public static class RenderGlobals
{
    // --- migrated managed fields ---------------------------------------------
    public static int SpriteSheetResHandle;     // 'spïn' sheet resource Handle scratch
    public static short SpriteLoadSlotIndex;       // sprite/icon load-slot counter
    public static short SpriteLoadSlotIndexSaved;  // saved copy → the i*0x1a slot index
    // PaletteStateRecord: GONE (G6j) - the record is Graphics.Model.PaletteState.
    public static byte ColorQuickDrawAvailable;   // colour-QuickDraw flag (never set → 0)
    public static byte DrawGateFlag;              // in-spaceport / draw-busy gate byte (unified to
                                                  // the direct-byte model; the decompile's pointer
                                                  // deref is dropped — the pointer was never set up)

    // --- address documentation only (not used by call sites) ----
    public const int SpriteSheetResHandleSlot = 0x1008709c;
    public const int SpriteLoadSlotIndexSlot = 0x1008a4e8;
    public const int SpriteLoadSlotIndexSavedSlot = 0x1008a4ea;
    public const int PaletteStateRecordSlot = 0x100823f0;
    public const int ColorQuickDrawAvailableFlagSlot = 0x100823f4;
    public const int DrawGateFlagSlot = 0x10080c10;

    // (The game-window bounds ptr cell 0x100811bc and the Rect behind it are
    // managed: Core.Model.GameWindowGlobals.GameWindowBounds.)

    // --- HUD-redraw-scheduler scalar cluster — MANAGED fields ----------------
    // Were the data-seg scalars at 0x10086d98..0x10086d9f reached through the
    // PEF-relocated ptr cells 0x100811c4..d4. The old "not migratable" rationale
    // — a long Pascal spob-desc string at 0x10086d84 legitimately OVERRAN into
    // these bytes on the Mac — is RETIRED: the desc buffer is the managed
    // DialogScratch.SpaceportDescText (G5d), so nothing can overrun the cluster
    // any more (the overlap was an accident of raw adjacency, not a consumer).
    public static short RadarHudAnimTick;        // was *(*0x100811c4) @0x10086d9c — 0..0x1e HUD anim tick
    public static byte RadarHudJamFlag;         // was *(*0x100811c8) @0x10086d9f — live debug-target key flag
    public static byte HudCachedJamFlag;        // was *(*0x100811cc) @0x10086d9e — cached copy (toc-0x7494)
    public static short HudCachedTargetShield;   // was *(*0x100811d0) @0x10086d9a — 0x8001 = force-redraw sentinel
    public static short HudCachedTargetClass;    // was *(*0x100811d4) @0x10086d98 — cached target ship class

    // Target-info DEBUG panel gate (rtoc-0x76dc): a DOUBLE deref of TOC pointer cell
    // 0x10080F84, which points at the uninitialised BSS byte unk_E021D (`.space 1` = 0).
    // All 8 load sites READ it; nothing anywhere WRITES it, and it is unreachable by addi
    // displacement — so the "debug target panel" enable is PERMANENTLY 0 and the developer
    // target-state readout is DEAD CODE in shipping 1.0.2 (verified in ASM; DEV_DEBUG_CODE.md).
    // The faithful default is therefore 0. (An earlier comment here wrongly cited cell
    // 0x10081984 → code byte 0x1000464c; both addresses are fictional — the real reason it
    // reads 0 is the uninitialised BSS byte, not a code-byte deref.)
    //
    // APPROVED DEVIATION (2026-07-03, user-approved): a --debug / --target-debug-panel
    // host flag forces the readout ON for development. Mac-INVISIBLE host substrate — nothing
    // inside the ported program observes the CLI flag; with no flag the default stays 0.
    public static bool HostDebugPanelOverride;   // set by the host arg parser (Program.Main)
    public static byte TargetDebugPanelFlag => (byte)(HostDebugPanelOverride ? 1 : 0);

    // Radar jam static-colour table (was the heap array of 10 PixPat handles
    // behind ptr cell 0x10080d90 / toc-0x78d0): filled by LoadSpriteSheetsAndGWorlds
    // (GetPixPat 0x80..0x89 — a 0-stub today), read by DrawRadarHud's jam fill.
    public static readonly int[] RadarJamColorTable = new int[10];

    // --- The three fixed in-game/title offscreen GWorlds — MANAGED PORTS -----
    // Registered AT their legacy BSS slot addresses so the host pixmap keys
    // (slot+2: backdrop 0x1008f6ee, status panel 0x1008f6d2, secondary 0x1008f70a)
    // stay bit-identical — OverrideGameHost's BACKDROP RenderTarget registration
    // and TitleAdapter's panel scratch textures need no re-keying.
    // (note: the old inline GWorld record bytes ALIASED the host's anim sentinel/key
    // cells f6fe/f700, which are pure int VALUES, never memory reads — see
    // OverrideGameHost.AnimPixmapKey).
    //
    // Deviation (documented): the Mac InitGameOffscreenBuffers allocates real
    // software GWorlds here and stores their port in the record — in the port that
    // re-pointed the record OFF the host keys, so every "into the backdrop"
    // draw silently fell back to the screen target. The managed ports keep the
    // slot-keyed model the host was designed around.
    public static readonly MacGrafPort BackdropPort = MacGrafPorts.RegisterAt(0x1008f6ec);
    public static readonly MacGrafPort StatusPanelPort = MacGrafPorts.RegisterAt(0x1008f6d0); // PICT 128
    public static readonly MacGrafPort SecondaryPanelPort = MacGrafPorts.RegisterAt(0x1008f708); // PICT 160

    // Port-handle values (== the legacy slot addresses, so `+ 2` forms still
    // produce the registered host keys).
    public static int BackdropGWorld => BackdropPort.Handle;
    public static int StatusPanelBgGWorld => StatusPanelPort.Handle;
    public static int SecondaryPanelGWorld => SecondaryPanelPort.Handle;

    // The backdrop GWorld's portRect as a managed {top,left,bottom,right} copy.
    public static short[] BackdropPortRect => BackdropPort.PortRectShorts();

    public const int PilotInfoLabelStr = 0x10082d47;   // &DAT data-seg string (B6)

    // Armor-bar fill PixPat (was the heap int slot behind ptr cell 0x10080d8c /
    // GameToc-0x78d4): the 'ppat' 200 Handle — LoadSpriteSheetsAndGWorlds stores
    // it (GetPixPat is a 0-stub today), DrawPlayerShieldBar's armor branch reads.
    public static int ArmorBarPixPat;
}
