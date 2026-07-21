namespace OpenEV.Override.Ports.Core.Model;

// 'ëbug' resource bit indices (a packed short[32]; only 0..15 are read anywhere).
// Ambrosia's per-subsystem developer debug/test toggles — each name reflects only
// what its call site's own established comment already says. Bit 0xc is read for
// two unrelated purposes (RunMainGameLoop's live relayout check and PilotSave's
// boot-cached save guard, same underlying resource bit) so it keeps a neutral
// hex name rather than picking one, same convention as MacKeycode's Key0xNN.
public enum BugBit : short
{
    NoOverwriteExistingData = 0x0,  // LoadSpobAndStellarResources: skip the whole spob/syst/outfit/etc. load if already populated
    SkipBarPersonMissionLoad = 0x1,  // LoadBarPersonResources: skip the whole bar-person/mission-avail load
    SkipSpriteSheetLoad = 0x2,  // LoadSpriteSheetsAndGWorlds: skip the sprite-sheet/GWorld load
    HiRes = 0x3,  // SpriteFrameTables.HiResFlag
    DebugStackSpaceDump = 0x4,  // LoadSpriteSheetsAndGWorlds: DebugStr the free stack space after graphics load; ALSO OriginalGameStateTotalBytes' after-table-creation dump
    SkipCargoSpaceReroll = 0x5,  // LoadBarPersonResources: skip the CargoSpaceRequired reroll (ResolveSignedRollShort)
    SkipMissionAvailFieldRead = 0x6,  // LoadBarPersonResources: skip reading the mission-avail resource's fields into the table row
    SkipMissionNameCopy = 0x7,  // LoadBarPersonResources: skip copying the mission resource's name into the name table
    SkipMissionAvailResLoad = 0x8,  // LoadBarPersonResources: skip the 512-entry mission-avail resource load loop
    SkipMissionTableReset = 0x9,  // LoadBarPersonResources: skip the mission-state/control-bit table reset
    SkipMainGameLoop = 0xA,  // RunGameSessionLauncher: skip RunMainGameLoop entirely
    NoStarfieldScroll = 0xB,  // RunMainGameLoop: disable the starfield scroll
    Bit0xC = 0xC,  // RunMainGameLoop (live): disable window-region relayout; ALSO PilotSave's boot-cached save guard
    SavePilotFileGuard = 0xD,  // SavePilotFile's boot-cached save guard (only known reader)
    SkipTooltipDescSubstitution = 0xE,  // SubstituteMissionDescTags.SkipSubstitution
    MissionAvailOverride = 0xF,  // "everyone shows up" mission-availability override
}
