namespace OpenEV.Override.Ports.Title.Model;

// MANAGED home for the shared title-screen globals — the records the Mac kept
// behind the _DAT_1008110x/_DAT_100810xx pointer cells and their TOC-relative
// aliases (GameToc 0x10088660, so e.g. toc-0x7540 == DAT_10081120). The port's fake
// _toc base (0x10301000) had SPLIT every such alias pair into two scratch
// cells, so writers through one alias never reached readers of the other
// (orb-frame/last-hovered resets, the ButtonRevealPulse re-arm, the PICT-8000
// handle). One C# field per datum unifies each alias pair again — the former
// address(es) each field replaces are noted on its own declaration below.
// Rects are {top,left,bottom,right} short[4].
//
// (DAT_10080f60, the quit flag, is now the plain managed bool
// EvoGlobals.QuitRequested — no EvoMemory pointer cell behind it anymore —
// the host + in-game loop poll it there.)
public static class TitleScreenGlobals
{
    // PICT 8000 (title backdrop) resource Handle; 0 = released.
    public static int Pict8000Handle; // DAT_10081120 (toc-0x7540)

    // Button-row reveal/pulse gate: set by InitTitleBackdrop, cleared by the
    // first title click (DispatchTitleEvent) or a held button in
    // DrawClosedButtons. Gates AnimateRowReveal AND the closed-button overlay.
    public static bool ButtonRevealPulse; // DAT_10081108 (toc-0x7558)

    // TitleMainLoop's per-iteration repaint gate. FAITHFULLY DEAD: the Mac
    // byte lives in zero-filled BSS (0x100e0f90, via pointer cell DAT_100810f0
    // — PEF data-seg dump) and NOTHING in the binary ever writes it, so the
    // branch never fires; the pilot panel paints via InvalRect → updateEvt →
    // DrawPilotInfo instead. An earlier port seeded this TRUE (updateEvts
    // didn't exist yet), turning the dead branch into a full-screen repaint
    // EVERY loop iteration — which erased the hover orb right after each
    // idle tick drew it, so the orb never showed.
    public static bool PilotInfoDirty; // DAT_100810f0 → BSS byte 0x100e0f90

    // App-in-background flag (Mac suspend/resume). Nothing in the port suspends, so
    // it only ever flips false — the safe foreground default.
    public static bool InBackground; // DAT_100810fc (toc-0x7564)

    // One-shot latch for the title cheat-chord sound (re-armed by the
    // shipyard's ship-buy path in RunShipyardDialog).
    public static bool CheatSoundPlayed; // DAT_10080ff4 (toc-0x766c)

    // Hover-orb animation state (HoverOrbDrawErase / InitTitleRects).
    public static short OrbAnimFrame;          // DAT_100810bc (toc-0x75a4); atlas frame 0..3
    public static short LastHoveredOrb = -1;   // DAT_100810b4 (toc-0x75ac); -1 = none
    public static int OrbAnimTickTimer;      // 0x100810b8 (toc-0x75a8); TickCount of the last frame advance

    // The two regions InitTitleBackdrop allocates (NewRgn is a 0-returning
    // shim; kept for shape fidelity).
    public static int RgnHandleA; // *(toc-0x7554)
    public static int RgnHandleB; // *(toc-0x7554) + 4

    // About-EVÉ speech easter-egg one-shot flag; InitTitleBackdrop re-arms it.
    public static byte SpeechEasterEggFlag; // **(toc-0x7544)

    // 640×480 inner-arena rect centred in the main port (InitTitleBackdrop).
    public static short[] InnerArenaRect = new short[4]; // DAT_10081110

    // Backdrop rect = the full port rect (DrawPicture dst for PICT 8000).
    public static short[] BackdropRect = new short[4]; // DAT_10081104 (toc-0x755c)

    // The 6 button hit-test rects and 6 orb destination rects (3 rows × 2
    // cols), laid out by InitTitleRects.
    public static short[][] ButtonRects = NewRectArray(); // DAT_10081114
    public static short[][] OrbRects = NewRectArray(); // DAT_100810c0

    private static short[][] NewRectArray()
    {
        var a = new short[6][];
        for (int i = 0; i < 6; i++) a[i] = new short[4];
        return a;
    }
}
