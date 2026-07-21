using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_10078d88 (EV Override-11.c lines 51392-51411): stream `len` bytes of the parked
// partial resource into `dest` (a raw caller buffer — the QD getPic bottleneck's
// parameter, kept raw at that boundary) and advance the read cursor. The handle/cursor
// state is the managed ResourceGlobals.PartialResStream* pair.
//
// No live caller today: this is the target of the StreamGetPicProc routine descriptor
// DrawPictResource installs on its progressive-load path, which is dead in the port
// (see DrawPictResource's header) — kept faithful for when that path is revisited.
public static class ReadPartialResourceAdvance
{
    public static void Run(int dest, short len)
    {
        MacToolbox.ReadPartialResource(ResourceGlobals.PartialResStreamHandle,
            ResourceGlobals.PartialResStreamCursor, dest, (int)len);
        ResourceGlobals.PartialResStreamCursor += len;
    }
}
