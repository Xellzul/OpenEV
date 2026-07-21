using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007b2f4 (EV Override-11.c 52767-53127): (re)build/resize the game render
// window, clamp the drawable rect, build the pixmap row table, install the depth blitters.
// The render context (piVar3 = _DAT_10080d08) lives in GlobalState; the Mac objects it uses
// (QuickDraw globals, sprite-port portBits) stay at the toolbox boundary on raw pointers.
public static class InitRenderWindow
{
    // TOC cell 0x100812c8: sprite-render-node record byte size (92), read by FUN_1007c06c as
    // the NewPtr size. The port allocs SpriteNode.Capacity instead, so this seed is inert.
    private static int _spriteNodeByteSize;

    // TOC flag 0x10081ab0: "the game window has really been created" (guards the reuse path).
    private static byte _gameWindowCreated;

    // refreshPixMap/clampToBounds are the real 9th/10th args (the decompile truncated the signature
    // to 8; the ASM wins). Both callers pass arg9=0/arg10=1 -> refreshPixMap:false, clampToBounds:true.
    public static void Run(short scrollX, short scrollY, short[] boundsRect, int existingWindow, int targetDevice, bool forceRebuild, bool centerFlag, bool useFullContentArea, bool refreshPixMap, bool clampToBounds)
    {
        ResourceGlobals.ToolboxShimInitFlag = 1;

        if (GlobalState.TempRegion == 0)
            GlobalState.TempRegion = MacRegions.New().Handle;

        short savedDepth = GlobalState.RenderMode;
        // Pin colour QuickDraw on — the ONLY store to ctx+0xc6 binary-wide, so every
        // `ColorQuickDrawFlag == 0` (B&W) branch below is dead-but-faithful.
        GlobalState.ColorQuickDrawFlag = 1;
        SaveCurrentPortAndDevice.Run(out int savedPort, out int savedDevice);
        if (GlobalState.ColorQuickDrawFlag == 0)
            GlobalState.NonColorQuickDrawFlag = 1;

        if (_spriteNodeByteSize == 0)
            _spriteNodeByteSize = 92;

        // (The Mac filled a 10-entry trap dispatch table behind *0x100812c4 with NGetTrapAddress
        // results; the host's blits are managed and nothing reads the table, so the writes are gone.)

        if (GlobalState.ColorQuickDrawFlag == 0)
            GlobalState.GDevice = 0;
        else if (targetDevice == 0)
            GlobalState.GDevice = MacToolbox.GetMainDevice();
        else
            GlobalState.GDevice = targetDevice;
        if (GlobalState.ColorQuickDrawFlag != 0 && GlobalState.GDevice != MacToolbox.GetMainDevice())
            forceRebuild = true;

        CacheCurrentDeviceFields.Run();
        short adjust = !forceRebuild ? (short)MenuBarHeight.Run() : (short)0;   // low-mem MBarHeight (0 in the host)

        int drawTopLeft = ((boundsRect[0] & 0xffff) << 16) | (boundsRect[1] & 0xffff);
        int drawBotRight = ((boundsRect[2] & 0xffff) << 16) | (boundsRect[3] & 0xffff);

        int winTopLeft;
        int winBotRight;
        if (existingWindow == 0)
        {
            winTopLeft = drawTopLeft;
            winBotRight = drawBotRight;
            if (useFullContentArea)
            {
                winBotRight = GlobalState.WindowBoundsBotRightPacked;
                winTopLeft = ((GlobalState.WindowBoundsTop + adjust) << 16)
                            | (GlobalState.WindowBoundsLeft & 0xffff);
            }
            if (GlobalState.ActivePortPixmap != 0)
            {
                if (_gameWindowCreated == 0)
                    GlobalState.ActivePortPixmap = 0;
                else
                {
                    MacToolbox.MoveWindow(GlobalState.ActivePortPixmap, (int)(short)winTopLeft, (int)(short)(winTopLeft >> 16), 0);
                    MacToolbox.SizeWindow(GlobalState.ActivePortPixmap, (int)(short)winBotRight - (int)(short)winTopLeft,
                                          (int)(short)(winBotRight >> 16) - (int)(short)(winTopLeft >> 16), 0);
                }
            }
            if (GlobalState.ActivePortPixmap == 0)
            {
                short[] newBounds = { (short)(winTopLeft >> 16), (short)winTopLeft, (short)(winBotRight >> 16), (short)winBotRight };
                // title = empty Pascal string (was toc-0x61ee); the New[C]Window shims never read it.
                GlobalState.ActivePortPixmap = GlobalState.ColorQuickDrawFlag == 0
                    ? MacToolbox.NewWindow(0, newBounds, 0, 0, 2, -1, 0, 0)
                    : MacToolbox.NewCWindow(0, newBounds, 0, 0, 2, -1, 0, 0);
                _gameWindowCreated = 1;
            }
        }
        else
        {
            GlobalState.ActivePortPixmap = existingWindow;
            MacToolbox.GetPort(savedPort);   // (savedPort already holds the save; boundary no-op)
            MacToolbox.SetPort(GlobalState.ActivePortPixmap);
            MacToolbox.GetPortRect(GlobalState.ActivePortPixmap, out winTopLeft, out winBotRight);
            MacToolbox.LocalToGlobal(winTopLeft);
            MacToolbox.LocalToGlobal(winBotRight);
            MacToolbox.LocalToGlobal(drawTopLeft);
            MacToolbox.LocalToGlobal(drawBotRight);
            MacToolbox.SetPort(savedPort);
        }

        // Host bridge (bug-class #8): the NewCWindow handle is host-unbacked. Writes already fall
        // back to the screen target, but CopyBits that SAMPLE it as a source got null and no-op'd
        // (broke the title button-reveal). Alias its +2 read-key to the screen RT, only when
        // unbacked so the GAME/SCREEN sentinels are never clobbered.
        var screenRt = MacToolbox.ResolveRenderTarget(MacToolbox.ScreenPixmapSentinel + 2);
        if (screenRt is not null && GlobalState.ActivePortPixmap != 0 &&
            MacToolbox.ResolveRenderTarget(GlobalState.ActivePortPixmap + 2) is null)
        {
            MacToolbox.RegisterRenderTarget(GlobalState.ActivePortPixmap + 2, screenRt);
        }

        if (centerFlag)
        {
            short width = (short)((short)drawBotRight - (short)drawTopLeft);
            short height = (short)((short)(drawBotRight >> 16) - (short)(drawTopLeft >> 16));
            short gapH = (short)((GlobalState.WindowBoundsRight - GlobalState.WindowBoundsLeft) - width);
            short centreH = (short)(GlobalState.WindowBoundsLeft + (gapH >> 1) + ((gapH < 0 && (gapH & 1) != 0) ? 1 : 0));
            short gapV = (short)(((GlobalState.WindowBoundsBottom - adjust) - GlobalState.WindowBoundsTop) - height);
            short centreV = (short)(GlobalState.WindowBoundsTop + (gapV >> 1) + ((gapV < 0 && (gapV & 1) != 0) ? 1 : 0) + adjust);
            drawTopLeft = ((centreV & 0xffff) << 16) | (centreH & 0xffff);
            drawBotRight = (((height + centreV) & 0xffff) << 16) | ((width + centreH) & 0xffff);
        }

        // clampToBounds (true for both callers): clamp the draw rect into the window bounds, then
        // (2nd block) align its L/R edges to 16/8 px.
        if (clampToBounds)
        {
            if ((short)drawTopLeft < (short)winTopLeft) drawTopLeft = (drawTopLeft & ~0xffff) | (winTopLeft & 0xffff);
            if ((short)winBotRight < (short)drawBotRight) drawBotRight = (drawBotRight & ~0xffff) | (winBotRight & 0xffff);
            if ((short)(drawTopLeft >> 16) < (short)(winTopLeft >> 16)) drawTopLeft = (winTopLeft & ~0xffff) | (drawTopLeft & 0xffff);
            if ((short)(winBotRight >> 16) < (short)(drawBotRight >> 16)) drawBotRight = (winBotRight & ~0xffff) | (drawBotRight & 0xffff);
        }

        // Faithful leftover: offset a bounds copy to origin, result unused.
        short[] wb = GlobalState.WindowBoundsRect;
        MacToolbox.OffsetRect(wb, (short)-GlobalState.WindowBoundsLeft, (short)-GlobalState.WindowBoundsTop);

        if (clampToBounds)
        {
            if ((short)drawTopLeft < GlobalState.WindowBoundsLeft) drawTopLeft = (drawTopLeft & ~0xffff) | (GlobalState.WindowBoundsLeft & 0xffff);
            if (GlobalState.WindowBoundsRight < (short)drawBotRight) drawBotRight = (drawBotRight & ~0xffff) | (GlobalState.WindowBoundsRight & 0xffff);
            if ((short)(drawTopLeft >> 16) < (short)(GlobalState.WindowBoundsTop + adjust)) drawTopLeft = (((GlobalState.WindowBoundsTop + adjust) & 0xffff) << 16) | (drawTopLeft & 0xffff);
            if (GlobalState.WindowBoundsBottom < (short)(drawBotRight >> 16)) drawBotRight = ((GlobalState.WindowBoundsBottom & 0xffff) << 16) | (drawBotRight & 0xffff);
            short alignL = (short)(MacToolbox.BitAnd((int)(short)((short)drawTopLeft + 0xf), -16));
            drawTopLeft = (drawTopLeft & ~0xffff) | (alignL & 0xffff);
            short alignR = (short)(MacToolbox.BitAnd((int)(short)drawBotRight, -8));
            drawBotRight = (drawBotRight & ~0xffff) | (alignR & 0xffff);
        }
        if ((short)drawBotRight < (short)drawTopLeft) drawBotRight = (drawBotRight & ~0xffff) | (drawTopLeft & 0xffff);
        if ((short)(drawBotRight >> 16) < (short)(drawTopLeft >> 16)) drawBotRight = (((drawTopLeft >> 16) & 0xffff) << 16) | (drawBotRight & 0xffff);

        GlobalState.InnerRight = (short)((short)drawBotRight - (short)drawTopLeft);                 // width
        GlobalState.InnerBottom = (short)((short)(drawBotRight >> 16) - (short)(drawTopLeft >> 16)); // height
        SetGamePortAndDevice.Run();

        // Drawable rect + its GlobalToLocal copy (unused result), then offset to origin.
        int localTopLeft = drawTopLeft, localBotRight = drawBotRight;
        MacToolbox.GlobalToLocal(localTopLeft);
        MacToolbox.GlobalToLocal(localBotRight);
        GlobalState.DrawRectTopLeftPacked = drawTopLeft;
        GlobalState.DrawRectBotRightPacked = drawBotRight;
        short[] dr = GlobalState.DrawRect;
        MacToolbox.OffsetRect(dr, (short)-GlobalState.WindowBoundsLeft, (short)-GlobalState.WindowBoundsTop);
        GlobalState.DrawRect = dr;

        // Unconditional clamp (decompile 52991-53008): NOT the clampToBounds path — always runs.
        short[] winLocal = { (short)(winTopLeft >> 16), (short)winTopLeft, (short)(winBotRight >> 16), (short)winBotRight };
        MacToolbox.OffsetRect(winLocal, (short)-GlobalState.WindowBoundsLeft, (short)-GlobalState.WindowBoundsTop);
        short wlTop = winLocal[0], wlLeft = winLocal[1], wlBottom = winLocal[2], wlRight = winLocal[3];
        if (GlobalState.DrawRectLeft < wlLeft) GlobalState.DrawRectLeft = wlLeft;
        if (wlRight < GlobalState.DrawRectRight) GlobalState.DrawRectRight = wlRight;
        if (GlobalState.DrawRectTop < wlTop) GlobalState.DrawRectTop = wlTop;
        if (wlBottom < GlobalState.DrawRectBottom) GlobalState.DrawRectBottom = wlBottom;
        if (GlobalState.DrawRectBottom <= GlobalState.DrawRectTop)
            GlobalState.DrawRectBottom = (short)(GlobalState.DrawRectTop + 1);
        GlobalState.ScrollOffsetX = (int)(short)(-GlobalState.WindowBoundsLeft - (short)drawTopLeft);
        GlobalState.ScrollOffsetY = (int)(short)(-GlobalState.WindowBoundsTop - (short)(drawTopLeft >> 16));
        MacToolbox.SetOrigin((int)(short)winTopLeft - (int)(short)drawTopLeft, (int)(short)(winTopLeft >> 16) - (int)(short)(drawTopLeft >> 16));

        // DrawRectGlobal = the window's portRect made global; LocalToGlobal is a no-op host shim so
        // mirror the (already global) window bounds. (Currently unconsumed by the port.)
        GlobalState.DrawRectGlobalTop = (short)(winTopLeft >> 16);
        GlobalState.DrawRectGlobalLeft = (short)winTopLeft;
        GlobalState.DrawRectGlobalBottom = (short)(winBotRight >> 16);
        GlobalState.DrawRectGlobalRight = (short)winBotRight;

        if (GlobalState.ColorQuickDrawFlag == 0)
        {
            // DEAD (flag pinned to 1 above): the Mac B&W path read screen base/rowBytes off the QD
            // globals (0x10080dd8 +0x50/+0x54); unavailable in the true-color host, so a tripwire.
            throw new System.NotSupportedException(
                "InitRenderWindow: the B&W QD-globals path ran (flag pinning changed?) — re-derive.");
        }
        else
        {
            // Colour: screen pixmap = *(*(GDevice)+0x16), resolved at the boundary by GetDeviceScreenPixMap.
            MacToolbox.GetDeviceScreenPixMap(GlobalState.GDevice, out int screenBase, out short rowBytes);
            GlobalState.ScreenBaseAddr = screenBase;
            GlobalState.ScreenRowBytes = rowBytes;
            GlobalState.PixMapRowTableBase = BuildPixMapRowTable.Rebuild(
                GlobalState.PixMapRowTableBase, GlobalState.DrawRectTopLeftPacked,
                GlobalState.DrawRectBotRightPacked, screenBase,
                rowBytes, GlobalState.RenderMode);
        }

        GlobalState.PortTopLeftPacked = GlobalState.DrawRectTopLeftPacked;
        GlobalState.PortBotRightPacked = GlobalState.DrawRectBotRightPacked;
        short[] pr = GlobalState.PortRect;
        MacToolbox.OffsetRect(pr, (short)-GlobalState.PortLeft, (short)-GlobalState.PortTop);
        GlobalState.PortRect = pr;
        short adjH = (short)(GlobalState.DrawRectLeft + (GlobalState.InnerRight - GlobalState.DrawRectRight));
        if (0 < adjH)
        {
            pr = GlobalState.PortRect;
            MacToolbox.OffsetRect(pr, (short)((adjH >> 1) + ((adjH < 0 && (adjH & 1) != 0) ? 1 : 0)), 0);
            GlobalState.PortRect = pr;
        }
        short adjV = (short)(GlobalState.DrawRectTop + (GlobalState.InnerBottom - GlobalState.DrawRectBottom));
        if (0 < adjV)
        {
            pr = GlobalState.PortRect;
            MacToolbox.OffsetRect(pr, 0, (short)((adjV >> 1) + ((adjV < 0 && (adjV & 1) != 0) ? 1 : 0)));
            GlobalState.PortRect = pr;
        }

        InitSoundSubsystem.Run();

        // SetRect(l,t,r,b)=(0,0,66,64) -> {top 0,left 0,bottom 64,right 66}: 66w x 64h (do not swap).
        short[] gworldRect = { 0, 0, 64, 66 };
        ZeroPixMapBaseAndDispose.Run(ref GlobalState.ComposeScratchPort, ref GlobalState.ComposeScratchGDevice, ref GlobalState.ComposeScratchRowTable);
        ZeroPixMapBaseAndDispose.Run(ref GlobalState.SecondaryGWorldPort, ref GlobalState.SecondaryGWorldGDevice, ref GlobalState.SecondaryGWorldRowTable);
        if (GlobalState.SpriteGWorldPort != 0)
        {
            MacToolbox.ClosePort(GlobalState.SpriteGWorldPort);
            MacToolbox.DisposePtr(GlobalState.SpriteGWorldPort);
            GlobalState.SpriteGWorldPort = 0;
        }
        int gwTopLeft = (gworldRect[0] << 16) | (gworldRect[1] & 0xffff);
        int gwBotRight = (gworldRect[2] << 16) | (gworldRect[3] & 0xffff);
        DisposePixMapBaseWithAlignmentUnwind.Run(ref GlobalState.ComposeScratchPort, ref GlobalState.ComposeScratchGDevice, ref GlobalState.ComposeScratchRowTable, gwTopLeft, gwBotRight);
        DisposePixMapBaseWithAlignmentUnwind.Run(ref GlobalState.SecondaryGWorldPort, ref GlobalState.SecondaryGWorldGDevice, ref GlobalState.SecondaryGWorldRowTable, gwTopLeft, gwBotRight);

        // Sprite GWorld port: a 108-byte old-style GrafPort (raw heap block); its portBits fields are
        // the host-bridge key for sprite MASK buffers; portRect/visRgn route through the port accessors.
        GlobalState.SpriteGWorldPort = CheckedAllocClear.Run(108);
        MacToolbox.OpenPort(GlobalState.SpriteGWorldPort);
        int spritePort = GlobalState.SpriteGWorldPort;
        MacToolbox.SetPortRect(spritePort, gwTopLeft, gwBotRight);
        // (the Mac also seeded portBits bounds/rowBytes/baseAddr; the only reader is now a dead tripwire, gone.)
        MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(spritePort), gworldRect);
        MacToolbox.ClipRect(gworldRect);

        if (GlobalState.SpriteLoopValue == 0)
            GlobalState.SpriteLoopValue = 32;
        if (GlobalState.OffscreenGWorldA != 0)
            DisposeOffscreenGWorld.Run(GlobalState.OffscreenGWorldA, GlobalState.OffscreenGWorldADevice);
        GlobalState.OffscreenGWorldA = 0;

        if (refreshPixMap && GlobalState.ColorQuickDrawFlag != 0 && GlobalState.RenderMode == 4)
            RefreshOffscreenPixMap.Run();
        if (GlobalState.SpriteListHead2 != 0 && savedDepth != GlobalState.RenderMode)
            RerenderAllSpritesForCurrentDepth.Run();

        SetScrollViewPosition.Run((int)scrollX, (int)scrollY);

        if (GlobalState.ColorQuickDrawFlag == 0)
        {
            GlobalState.SpriteBlitterFrags[0] = InstallCodeFragmentFromHandle.Install(MacToolbox.Get1Resource(MacResType.SpriteBlitterPR, 0));
            GlobalState.SpriteBlitterFrags[1] = InstallCodeFragmentVariantB.Install(MacToolbox.Get1Resource(MacResType.SpriteBlitterPM, 0));
        }
        else
        {
            int[] depths = { 1, 2, 4, 8, 16, 32 };
            for (int i = 0; i < depths.Length; i++)
            {
                GlobalState.SpriteBlitterFrags[i * 2] = InstallCodeFragmentFromHandle.Install(MacToolbox.Get1Resource(MacResType.SpriteBlitterPR, depths[i]));
                GlobalState.SpriteBlitterFrags[i * 2 + 1] = InstallCodeFragmentVariantB.Install(MacToolbox.Get1Resource(MacResType.SpriteBlitterPM, depths[i]));
            }
        }

        SelectSpriteRenderersByDepth.Run();
        SetPortAndDevice.Run(savedPort, savedDevice);
    }
}
