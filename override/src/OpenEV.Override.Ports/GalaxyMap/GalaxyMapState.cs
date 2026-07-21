namespace OpenEV.Override.Ports.GalaxyMap;

// Managed home for the galaxy-map dialog globals. In the original these are PEF-relocated
// POINTER cells in the data segment (GameToc-relative slots) whose BSS targets hold the live
// values; every access in the decompile goes THROUGH the cell (`**(toc-0xNNNN)`).
//
//   slot        toc off   field here
//   0x10080cac  -0x79b4   ButtonPics[8]          map button PICT handles (2 zoom + 4 + 2 route)
//   0x10080cb0  -0x79b0   RouteActive            byte: a plotted route exists
//   0x10080cb4  -0x79ac   MinusEnabled           byte: shared +/- button-row '-' enable
//   0x10080cb8  -0x79a8   PlusEnabled            byte: shared +/- button-row '+' enable
//   0x10080cbc  -0x79a4   MapDialog              the +/- button-row / galaxy-map DialogPtr
//   0x10080f94  -0x76cc   ResetFlag              byte (TradeFilter clears each event)
//   0x10080f98  -0x76c8   PreviewSystem          short: mission-destination preview system (-1 none)
//   0x10080f9c  -0x76c4   CentredSystem          short: system the map view is centred on
//   0x10080fa0  -0x76c0   TradeKeyLock           byte: blocks map/trade nav-target changes
//   0x10080fa4  -0x76bc   PreviewTargetIcon      cicn 15001 handle (map overlay)
//   0x10080fa8  -0x76b8   MissionDestinationIcon cicn 15000 handle (map overlay)
//   0x10080fac  (route)   RouteList[16]          govt mission/strand destination systems, -1 free
//   0x10080fb0  -0x76b0   UpdateRgn              RgnHandle (NewRgn; ScrollRect update region)
//   0x10080fb4  -0x76ac   ScrollInProgress       byte: set around DrawGalaxyMap during scroll
//   0x10080fb8  -0x76a8   VestigialRgn76a8       RgnHandle cell (disposed on map close)
//   0x10080fbc  -0x76a4   VestigialFlag76a4      byte (cleared on map open)
//   0x10080fc0  -0x76a0   HandCursor             CURS 128 handle (map pan cursor)
//   0x10080fc4  (grid)    NebulaPicts            4 nebulas x 3 zoom tiers of PICT handles (9500..)
//   0x10080fc8  (zoom)    Zoom                   the live map zoom/scale DOUBLE
//   0x10080f88  (rects)   NebulaScratchRects     4 nebula Rects (CacheMapNebulaBackgrounds scratch)
public static class GalaxyMapState
{
    // Faithful BSS-zero init: the original double starts 0.0, so RunGalaxyMapDialog's entry check
    // (Zoom == ZoomResetThreshold) fires and seeds the default zoom. Don't pre-seed it here.
    public static double Zoom;

    public static int MapDialog;

    public static byte MinusEnabled;
    public static byte PlusEnabled;
    public static byte ResetFlag;

    public static byte RouteActive;
    public static readonly short[] RouteList = new short[16];

    public static short CentredSystem;
    public static byte TradeKeyLock;

    public static short PreviewSystem;

    public static int HandCursor;
    public static int MissionDestinationIcon;
    public static int PreviewTargetIcon;
    public static int UpdateRgn;
    public static int VestigialRgn76a8;
    public static byte ScrollInProgress;
    public static byte VestigialFlag76a4;

    public static readonly int[] ButtonPics = new int[8];

    // Index = nebula*3 + tier (tier 0/1/2 = near/mid/far by zoom).
    public static readonly int[] NebulaPicts = new int[12];

    // Each Rect is {top,left,bottom,right} shorts.
    public static readonly short[][] NebulaScratchRects =
        { new short[4], new short[4], new short[4], new short[4] };
}
