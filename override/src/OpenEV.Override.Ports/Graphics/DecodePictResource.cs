using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Decompile: EV Override-11.c lines 51634-51687.
//
// Despite the legacy "Resource" name (and its old Resource/ folder — moved here since every
// real collaborator is a Graphics/ GWorld primitive, not resource-fork I/O), this CREATES an
// offscreen colour GWorld pair matching the screen GDevice's depth + colour table
// (NewOffscreenColorPort), locks the primary pixmap, and caches the bounds/rowBytes + builds
// the pixmap row table into the GWorld record. Returns 0 on success, else the OSErr from
// NewOffscreenColorPort or a failed pixmap lock.
//
// The GWorld record is 7 fields: [0]=port, [1]=gdevice, [2]=rowTable, [3]=portRectTopLeft,
// [4]=portRectBotRight, [5]=pixBase (device pix base), [6]=rowBytes. RunCore operates on those
// fields by ref — no unmanaged record block; the pixmaps and ports it walks are genuine Mac
// HANDLES/structs (handle double-deref), reached through MacToolbox.
public static class DecodePictResource
{
    // Managed overload: the GWorld sub-record {port, gdevice, rowTable} is held in GlobalState
    // fields (by ref) and the bounds is a managed short[4] {top,left,bottom,right}.
    //
    // The created GWorld's portRect (param_1[3]/param_1[4] in FUN_10079468) is threaded back
    // out as `portRectTopLeft`/`portRectBotRight` — callers (e.g. SetScrollViewPosition) seed
    // their record's stage Rect from these; don't collapse them to discarded locals, or the
    // stage Rect degenerates to a zero-height Rect. Record fields [5]/[6] (device pix base +
    // rowBytes) have no consumer in the managed pixmap model.
    public static int Run(ref int port, ref int gdevice, ref int rowTable, short[] boundsRect,
                          out int portRectTopLeft, out int portRectBotRight)
    {
        int boundsTopLeft = (boundsRect[0] << 16) | (boundsRect[1] & 0xffff);
        int boundsBotRight = (boundsRect[2] << 16) | (boundsRect[3] & 0xffff);
        portRectTopLeft = 0;
        portRectBotRight = 0;
        int pixBase = 0;
        short rowBytes = 0;
        return RunCore(ref port, ref gdevice, ref rowTable, ref portRectTopLeft, ref portRectBotRight,
                       ref pixBase, ref rowBytes, boundsTopLeft, boundsBotRight);
    }

    // Managed slot-record overload (Graphics.Model.SlotGWorlds.Sprite — LoadIconPairForSlot):
    // thread the 7 record fields through the core by ref, no raw record address.
    public static int Run(SlotGWorldRecord slot, int boundsTopLeft, int boundsBotRight)
        => RunCore(ref slot.Port, ref slot.GDevice, ref slot.RowTable,
                   ref slot.BoundsTopLeftPacked, ref slot.BoundsBotRightPacked,
                   ref slot.PixBase, ref slot.RowBytes,
                   boundsTopLeft, boundsBotRight);

    private static int RunCore(ref int port, ref int gdevice, ref int rowTable,
                               ref int portRectTopLeft, ref int portRectBotRight,
                               ref int pixBase, ref short rowBytes,
                               int boundsTopLeft, int boundsBotRight)
    {
        int savedGDevice = MacToolbox.GetGDevice();
        int[] savedPort = new int[2];
        MacToolbox.GetPort(savedPort);
        rowTable = 0;

        // KNOWN DEVIATION (unresolved — NOT verified faithful, not an accepted substitution):
        // the original gates on the SCREEN GDevice (ctx+0x04 = GlobalState.GDevice, which IS
        // real and populated by InitRenderWindow from MacToolbox.GetMainDevice() — NOT a stub)
        // and reads that device's LIVE pixel depth + colour table. This instead hardcodes
        // srcDepth=8 + GetCTable(8), gated on GetCTable's own null rather than
        // GlobalState.GDevice==0. A faithful path looks plumbable (GetDevicePMapHandle already
        // exists privately in MacToolbox) but depth/ctab-equivalence to GlobalState.GDevice is
        // unverified and the fatal-exit gate condition would change — NOT fixed here (this pass
        // is style-only). TODO: port FUN_10079468's real gate; needs its own verification +
        // explicit faithfulness sign-off before changing behavior.
        int srcDepth = 8;
        int srcCTab = MacToolbox.GetCTable(8);
        if (srcCTab == 0)
            // Message from data-seg cell 0x10085a4c (StaticData.UiErrorStrings[NoScreenErrorIndex]).
            FatalOutOfMemoryExit.Run(StaticData.UiErrorStrings[StaticData.NoScreenErrorIndex]);

        // NewOffscreenColorPort writes the primary/secondary ports only on success, so assign
        // port/gdevice only when it succeeds (matches the original's mid-function pictRec+0/+4
        // write — confirmed against its ASM: the write is reached only on the success branch).
        short err = (short)NewOffscreenColorPort.Run(boundsTopLeft, boundsBotRight,
                                                      srcDepth, srcCTab,
                                                      out int newPort, out int newGDevice);
        if (err != 0)
            return err;
        port = newPort;
        gdevice = newGDevice;

        // Lock the primary offscreen pixmap handle (port = the new port; +2 = its pixmap handle).
        int primaryPort = port;
        int primaryPixHandle = MacToolbox.GetPortPixMap(primaryPort);
        MacToolbox.MoveHHi(primaryPixHandle);
        MacToolbox.HLock(primaryPixHandle);
        if (MacToolbox.MemError() != 0)
            return MacToolbox.MemError();

        MacToolbox.SetGDevice(savedGDevice);
        MacToolbox.SetPort(savedPort[0]);

        // Cache the GDevice's pixmap base + the port bounds/rowBytes into the record.
        MacToolbox.GetDeviceScreenPixMap(gdevice, out pixBase, out rowBytes);
        MacToolbox.GetPortRect(primaryPort, out portRectTopLeft, out portRectBotRight);

        // NO-OP: BuildPixMapRowTable.Rebuild never refills the table in the managed renderer
        // (see its own doc — no ColorRowTable/MaskRowTable/PixMapRowTableBase reader remains;
        // the live blit path is the host RenderTarget bridge, not per-row addresses) — rowTable
        // always ends up 0. The call site is kept, matching FUN_1007933c's build-row-table step,
        // for record-shape completeness.
        rowTable = BuildPixMapRowTable.Rebuild(
            rowTable, portRectTopLeft, portRectBotRight, 0,
            MacToolbox.GetPixMapRowBytes(primaryPixHandle), (short)srcDepth);
        return 0;
    }
}
