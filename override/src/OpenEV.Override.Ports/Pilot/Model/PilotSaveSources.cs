namespace OpenEV.Override.Ports.Pilot.Model;

// Documentation home for the (now fully managed) pilot-save source globals.
//   0x10080b54 (the old PTR_DAT kill-count slot) → Systems.Model.GalaxyMapGlobals
//     .SystemStatusStore (short[1000]).
//   0x10080de0 (the old "GalaxyStateSlot" misname) → Core.Model.WorldState.StarDrift
//     (short[2], starfield drift pair).
public static class PilotSaveSources
{
    // 0x10080d0c (the old "PlayerDayCounterSlot" — a misname): -> player COMBAT RATING int.
    // Managed now: Core.Model.WorldState.PlayerCombatRating.
    // 0x10080ddc (StarJitterSlot): ptr cell to the star-jitter short[2]. Managed now:
    // Core.Model.WorldState.StarJitter (see OriginalGameStateTotalBytes).
    public const int StarJitterSlot = 0x10080ddc;
    // 0x100900fa is NOT a "galaxy save grid" / "per-syst pilot galaxy state" (that
    // description belongs to the short[1000] system-status store at 0x10080b54). It
    // is the player's owned-outfit count grid (short[128], indexed by outfit index
    // 0..0x7f); its home is Outfit.Model.OwnedOutfitGrid.Base. The dead, misnamed duplicate
    // const that lived here was removed — use Outfit.Model.OwnedOutfitGrid instead.
    // 0x1008210c (toc-0x6554, the old "ResetBlobSource") dumped from the PEF data
    // segment = ALL-NUL bytes (an empty string): the world-reset syst-name copy
    // and the DefaultGamePrefs/reset copies into MiddleNameBuffer just CLEAR
    // their destinations. Kept only for the buffer-clear sites' documentation.
    public const int EmptyStringSource = 0x1008210c;
    // 0x1009030c (toc+0x7cac) — the Str buffer between the (now managed) pilot-name
    // and ship-name buffers; only ever CLEARED, NO reader — the clear sites are
    // comments now (const removed).
}
