namespace OpenEV.Platform.Toolbox;

// Mac resource OSTypes (4-byte big-endian tags) the game loads via GetResource.
// The underlying value is the OSType as a big-endian uint; the name decodes the
// bytes (EVO's game types carry Ambrosia's signature umlauts, e.g. 'shïp', and
// 0x91/0x95/0x9a/0x9f/0xd8 are MacRoman ë/ï/ö/ü/ÿ). Pass these to
// MacToolbox.GetResource instead of a bare hex literal.
public enum MacResType : uint
{
    // EVO game data types.
    Bug      = 0x91627567,  // 'ëbug' — debug/config bit flags
    Ship     = 0x73689570,  // 'shïp'
    Sprite   = 0x7370956e,  // 'spïn' — sprite sheet
    Spob     = 0x73709a62,  // 'spöb' — stellar object (planet/station)
    System   = 0x73d87374,  // 'sÿst' — star system
    Weapon   = 0x77916170,  // 'wëap'
    Outfit   = 0x6f9f7466,  // 'oütf'
    Govt     = 0x679a7674,  // 'gövt' — government
    Person   = 0x70917273,  // 'përs' — bar person
    Dude     = 0x649f6465,  // 'düde' — NPC ship class
    Fleet    = 0x666c9174,  // 'flët'
    Junk     = 0x6a9f6e6b,  // 'jünk' — spaceport commodity
    Mission  = 0x6d95736e,  // 'mïsn'
    Nebula   = 0x6e916275,  // 'nëbu'
    Oops     = 0x9a6f7073,  // 'öops' — spaceport NPC / commodity-price event
    // 'dëqt' — QuickTime-movie descriptor (resource NAME = the .mov filename, res+0 =
    // flags). NOT a cargo table (commodities are 'jünk' = Junk). Base EVO 1.0.2 ships
    // ZERO of these (verified across all data forks + plug-ins), so PlayMovieById never
    // matches and every intro/mission "movie" falls back to its dësc text + PICT.
    Movie    = 0x64917174,
    Spit     = 0x73709574,  // 'spït' — boot progress-bar total (AnimateBootProgressBar)
    Desc     = 0x64917363,  // 'dësc' — description text (LoadDescriptionText)
    PrefsFile = 0x4d70a8c4, // 'Mp¨Ä' — EV Override prefs resource
    PilotRecord = 0x4f70954c,   // 'OpïL' — pilot save-file record (id 0x80 = main, 0x81 = aux)
    VersionStamp = 0x79918aa8,  // 'yëä®' — saved-game version stamp (id 0x80, one int16)

    // Standard Mac Toolbox types.
    Snd            = 0x736e6420,  // 'snd ' — sampled-sound resource (LoadSndResource)
    Pict           = 0x50494354,  // 'PICT'
    Dialog         = 0x444c4f47,  // 'DLOG'
    DialogItemList = 0x4449544c,  // 'DITL'
    StringList     = 0x53545223,  // 'STR#'
    String         = 0x53545220,  // 'STR '
    Text           = 0x54455854,  // 'TEXT'
    ColorIcon      = 0x6369636e,  // 'cicn'
    PixelPattern   = 0x70706174,  // 'ppat' — colour pixel pattern (radar static, armor-bar fill)
    ColorTable     = 0x636c7574,  // 'clut' — colour lookup table (credits/intro palettes)
    Style          = 0x7374796c,  // 'styl'
    Menu           = 0x4d454e55,  // 'MENU' — Menu Manager menu (register pop-ups)
    ColorCursor    = 0x63727372,  // 'crsr' — colour cursor (register's custom pointer over general content)

    // EV Override depth-specific sprite-blitter PowerPC code fragments (id = pixel depth).
    SpriteBlitterPR = 0x5052426c, // 'PRBl'
    SpriteBlitterPM = 0x504d426c, // 'PMBl'
}
