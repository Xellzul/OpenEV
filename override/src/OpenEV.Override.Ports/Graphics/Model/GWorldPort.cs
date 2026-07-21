using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics.Model;

// Installs a pixel buffer into the main in-game render context's primary/secondary offscreen
// GWorld port, plus the PixMap rowBytes helper — low-level QuickDraw GWorld/PixMap struct ops.
// The render-context record (formerly _DAT_10080d08 / MainGameWindowSlot) lives in the typed
// GlobalState; every `ctx + 0xNN` field read/write routes through it. What stays raw are the
// genuine Mac heap pointers those fields hold (the GWorld port, its pixmap, the GDevice handle)
// and basePtr. Ports of FUN_1007a1b4 / FUN_1007a310 / FUN_10079670 + the render-context setters.
public static class GWorldPort
{
    // FUN_1007a1b4 — point the PRIMARY GWorld port (ctx+0x8a, GDevice ctx+0x8e) at the
    // `basePtr` pixel buffer over the packed bounds rect, set its PixMap/rowBytes and the
    // cached context fields (ctx+0x96/0x9a/0x9e/0xa2), make it current, and reset a too-small
    // clip. The bounds halves are the packed {top,left}/{bottom,right} ints, like NewGWorld's.
    public static void InstallPrimaryGWorldPort(int basePtr, int boundsTopLeft, int boundsBotRight)
    {
        int port = GlobalState.ComposeScratchPort;     // ctx+0x8a (a genuine Mac port ptr)
        InstallGWorldPortCore(port, GlobalState.ComposeScratchGDevice, basePtr, boundsTopLeft, boundsBotRight);

        GlobalState.PrimaryCacheBase = basePtr;
        GlobalState.PrimaryCacheTopLeftPacked = boundsTopLeft;
        GlobalState.PrimaryCacheBotRightPacked = boundsBotRight;
        GlobalState.PrimaryCacheRowBytes = Graphics.BuildSpriteScaleTable.PixMapRowBytes((short)boundsBotRight);

        ShrinkClipToBounds(port, boundsTopLeft, boundsBotRight);
    }

    // FUN_1007a310 — same for the SECONDARY port (ctx+0xa4, GDevice ctx+0xa8) and its cached
    // fields (ctx+0xb0/0xb4/0xb8/0xbc).
    public static void InstallSecondaryGWorldPort(int basePtr, int boundsTopLeft, int boundsBotRight)
    {
        int port = GlobalState.SecondaryGWorldPort;    // ctx+0xa4
        InstallGWorldPortCore(port, GlobalState.SecondaryGWorldGDevice, basePtr, boundsTopLeft, boundsBotRight);

        GlobalState.SecondaryCacheBase = basePtr;
        GlobalState.SecondaryCacheTopLeftPacked = boundsTopLeft;
        GlobalState.SecondaryCacheBotRightPacked = boundsBotRight;
        GlobalState.SecondaryCacheRowBytes = Graphics.BuildSpriteScaleTable.PixMapRowBytes((short)boundsBotRight);

        ShrinkClipToBounds(port, boundsTopLeft, boundsBotRight);
    }

    // Aim the port's managed pixmap at the external pixel buffer (LegacyBaseAddr — the buffer
    // itself is still a raw sprite-pipeline block), set bounds/rowBytes, mirror the rect into
    // the portRect and (when present) the GDevice's gdRect, and make the pair current.
    private static void InstallGWorldPortCore(int port, int gdevice, int basePtr,
                                              int boundsTopLeft, int boundsBotRight)
    {
        var pixMap = MacPixMaps.At(MacToolbox.GetPortPixMap(port));
        pixMap.LegacyBaseAddr = basePtr;
        pixMap.Pixels = null;
        pixMap.SetBounds(boundsTopLeft, boundsBotRight);
        // RowBytes stores the byte count only; the Mac struct's 0x8000 pixMap-flag bit is
        // dropped for the managed PixMap.
        pixMap.RowBytes = (ushort)(PixMapRowBytesWithFlag((short)boundsBotRight) & 0x3fff);
        MacToolbox.SetPortRect(port, boundsTopLeft, boundsBotRight);
        MacToolbox.SetPort(port);

        if (gdevice != 0)
        {
            if (MacGDevices.IsHandle(gdevice))
            {
                var dev = MacGDevices.At(gdevice);     // gdRect (was *GDHandle+0x22/+0x26)
                dev.RectTop = (short)(boundsTopLeft >> 16);
                dev.RectLeft = (short)boundsTopLeft;
                dev.RectBottom = (short)(boundsBotRight >> 16);
                dev.RectRight = (short)boundsBotRight;
            }
            else
            {
                // Dead: every GDevice here is a MacGDevices registry handle (the outer
                // gdevice != 0 guard + the managed branch above). A raw GDevice record means
                // a new GDevice kind appeared.
                throw new System.NotSupportedException(
                    "InstallGWorldPortCore: raw GDevice gdRect write — re-derive if non-registry GDevices return.");
            }
            MacToolbox.SetGDevice(gdevice);
        }
    }

    // If the port's clip region is smaller than the new bounds, reset it to the bounds rect.
    private static void ShrinkClipToBounds(int port, int boundsTopLeft, int boundsBotRight)
    {
        short bottom = (short)(boundsBotRight >> 16);
        short right = (short)boundsBotRight;
        int rgnHandle = MacToolbox.GetPortVisRgn(port);
        short rgnBottom, rgnRight;
        if (MacRegions.IsHandle(rgnHandle))
        {
            var rgn = MacRegions.At(rgnHandle);   // the port's visRgn rgnBBox
            rgnBottom = rgn.BBoxBottom;
            rgnRight = rgn.BBoxRight;
        }
        else
        {
            // Dead: GetPortVisRgn returns a MacRegions registry handle.
            throw new System.NotSupportedException(
                "ShrinkClipToBounds: raw region stub — re-derive if non-registry regions return.");
        }
        if (rgnBottom < bottom || rgnRight < right)
        {
            var boundsRect = new[] { (short)(boundsTopLeft >> 16), (short)boundsTopLeft, bottom, right };
            MacToolbox.RectRgn(rgnHandle, boundsRect);
            MacToolbox.ClipRect(boundsRect);
        }
    }

    // FUN_10079670 — PixMap rowBytes word for a `pixelWidth`-wide row, with the high bit
    // (0x8000) set to flag a PixMap (vs BitMap). Uses 1 bit/pixel when the context's mask
    // flag (ctx+0xc6) is 0, otherwise the main GDevice PixMap's pixelSize (depth).
    public static int PixMapRowBytesWithFlag(int pixelWidth)
    {
        int rowBytes;
        if (GlobalState.ColorQuickDrawFlag == 0)        // ctx+0xc6
        {
            rowBytes = (int)((pixelWidth + 0x1fU) >> 3 & 0x1ffc);
        }
        else
        {
            // Depth from the main GDevice's pixmap (the GDHandle -> gdPMap walk is the
            // toolbox accessor's job).
            MacToolbox.GetDevicePixMapFields(GlobalState.GDevice, out _, out _, out short depth);
            rowBytes = (int)((uint)(pixelWidth * depth + 0x1f) >> 3 & 0x1ffc);
        }
        return (short)(0x8000 | rowBytes);   // high bit flags a PixMap
    }

    // (FUN_10079ac8 InitAlignedBitmap — the cicn mask-bitmap path — is inert since GetCIcon
    // is a 0-stub, and was deleted with that family.)

    // FUN_1007a868 — copy a sprite FRAME's mask-BitMap fields into the render context's sprite
    // port (window+0xbe) and, if the port's clip region is smaller than the frame, reset the
    // clip to the frame rect. The sprite PORT (an old-style BitMap-headed GrafPort) stays raw;
    // its +2 baseAddr field is a host-bridge key shape.
    public static void InstallSpriteAsActivePort(int spriteFrame)
    {
        // Unreachable: the only Mac caller chain is the cicn family (LoadCIconToSprite,
        // tripwired — GetCIcon is a 0-stub). The Mac body: stuff the frame's mask-BitMap fields
        // into the sprite port's portBits (+2 baseAddr = MaskBase; +6 packed {rowBytes,
        // boundsTop}; +0xa packed {boundsLeft, boundsBottom}; +0xe boundsRight), copy
        // portBits.bounds (+8/+0xc) into the portRect (+0x10/+0x14), SetPort, and grow the clip
        // region to the frame rect when smaller (visRgn +0x18 walk). Re-derive vs FUN_1007a868
        // when cicn support lands.
        throw new System.NotSupportedException(
            "InstallSpriteAsActivePort: cicn path invoked (GetCIcon stub changed?) — re-derive FUN_1007a868.");
    }

    // FUN_100526cc — set up the three in-game offscreen GWorlds (backdrop + the two
    // status-panel GWorlds) and paint their PICT backgrounds.
    //
    // Deviation (documented at RenderGlobals): the Mac allocates real software GWorlds here
    // (FUN_1007962c -> NewGWorld) and stores the new port in the record. The game's three
    // GWorlds ARE host RenderTargets/scratch textures keyed slot+2, and storing a foreign port
    // in the record re-keyed every backdrop draw onto the screen-fallback path. The managed
    // ports registered AT the slots keep the host keys; this function only sets their portRects
    // + paints the PICTs. The alloc-failure exits collapse with them (a managed port can't fail
    // to exist).
    public static void InitGameOffscreenBuffers()
    {
        const int StatusPanelPictId = 128;
        const int SecondaryPanelPictId = 160;
        const int BlackColor = 33;   // QuickDraw blackColor

        // A local copy of the game-window bounds (managed GameWindowGlobals.GameWindowBounds,
        // seeded by SystemVersionCheck at boot step 3), moved to origin.
        var src = Core.Model.GameWindowGlobals.GameWindowBounds;
        var rect = new[] { src[0], src[1], src[2], src[3] };
        MacToolbox.OffsetRect(rect, (short)-rect[1], (short)-rect[0]);

        RenderGlobals.BackdropPort.SetPortRectPacked(
            (rect[0] << 16) | (ushort)rect[1], (rect[2] << 16) | (ushort)rect[3]);

        // Status panel = the 144px right strip (right = left + 0x90).
        rect[3] = (short)(rect[1] + 144);
        RenderGlobals.StatusPanelPort.SetPortRectPacked(
            (rect[0] << 16) | (ushort)rect[1], (rect[2] << 16) | (ushort)rect[3]);

        int pic = MacToolbox.GetPicture(StatusPanelPictId);
        if (pic != 0)
        {
            SetPortAndDevice.Run(RenderGlobals.StatusPanelBgGWorld, 0);
            MacToolbox.ForeColor(BlackColor);
            var panelRect = RenderGlobals.StatusPanelPort.PortRectShorts();
            MacToolbox.PaintRect(panelRect);
            // DrawPicture rect = {top, left, top + 480, right} — PICT 128 is 480 tall, drawn
            // 1:1 at the top of the strip.
            MacToolbox.DrawPicture(pic, new[]
            {
                panelRect[0], panelRect[1], (short)(panelRect[0] + 480), panelRect[3],
            });
            MacToolbox.HPurge(pic);
            MacToolbox.ReleaseResource(pic);
        }

        // Secondary panel = SetRect(0, 0, 53, 35) — 53 wide x 35 tall (PICT 160).
        RenderGlobals.SecondaryPanelPort.SetPortRectPacked(0, (35 << 16) | 53);
        pic = MacToolbox.GetPicture(SecondaryPanelPictId);
        if (pic != 0)
        {
            SetPortAndDevice.Run(RenderGlobals.SecondaryPanelGWorld, 0);
            MacToolbox.ForeColor(BlackColor);
            var panelRect = RenderGlobals.SecondaryPanelPort.PortRectShorts();
            MacToolbox.PaintRect(panelRect);
            MacToolbox.DrawPicture(pic, panelRect);
            MacToolbox.HPurge(pic);
            MacToolbox.ReleaseResource(pic);
        }
        Graphics.SetGamePortAndDevice.Run();
    }

    // Unused: the GWorld-allocation-failure tail of FUN_100526cc — tear down, hide the window,
    // and exit to shell with the out-of-memory alert. The managed-port setup above can no
    // longer fail; kept as a faithful port (data-seg string 0x10084805) for a real failure path.
    private static void BailToShell()
    {
        TearDownSavedPalette.Run();
        MacToolbox.HideWindow(GlobalState.ActivePortPixmap);   // *_DAT_10080d08 (ctx+0)
        Dialog.RestoreMacMenuBar.Run();
        MacToolbox.ShowCursor();
        Title.AlertModal_OneButton.Run(
            "Sorry, EV ran out of memory while creating an offscreen buffer. "
            + "Please increase EV’s memory allocation and try again.");
        Sound.TeardownSoundSubsystem.Run();
        MacToolbox.ExitToShell();
    }

    // ======================================================================
    // Game-window / render-context port setters.

    // The anim-scratch port VALUE (render context +0x38) the title button-reveal
    // (AnimateRowReveal / DrawClosedButtons / HitTestTitleButton) stages into and CopyBits
    // from. Staging is symmetric only when this port's +2 key is a host RenderTarget:
    // SetPort(port) routes the staging DrawPictures to port+2 and the blits sample port+2.
    //
    // The in-game anim GWorld (Enter-Ship -> OnEnterGameWorld) is the AnimPixmapSentinel, whose
    // +2 is the host ANIM RenderTarget. But boot's InitGalaxyMapWindow -> InitRenderWindow ->
    // SetScrollViewPosition leaves a REAL but host-UNBACKED offscreen GWorld here
    // (NewOffscreenColorPort returns a MacGrafPorts handle; no Rgba8Image is registered for it).
    // Staging into an unbacked port falls through to the SCREEN (the 480x59 strip lands at the
    // top-left) and the CopyBits source resolves to null. Use the live port only when
    // host-backed; otherwise fall back to the title ANIM RT (port+2 = AnimPixmapKey).
    public static int ScratchPort
    {
        get
        {
            int port = GlobalState.AnimScratchPort;   // ctx+0x38 (anim-scratch port)
            if (port != 0 && MacToolbox.ResolveRenderTarget(port + 2) != null)
                return port;
            return MacScratch.AnimScratchPixmap - 2;   // host ANIM RT key (port+2 = AnimPixmapKey)
        }
    }

    // FUN_1007aaf4 — make the anim-scratch port (render context +0x38, GDevice +0x3c) active.
    // The host-backed fallback lives in ScratchPort.
    public static void SetActivePortScratch()
    {
        SetPortAndDevice.Run(ScratchPort, GlobalState.AnimScratchGDevice);
    }

    // FUN_1007aacc — make the secondary game port (render context +0x1e, GDevice +0x22) active.
    public static void SetActivePortSecondaryGame()
    {
        SetPortAndDevice.Run(GlobalState.OffscreenGameGWorld, GlobalState.OffscreenGameGDevice);
    }

    // FUN_10078ae0 — store the current game-window pointer in the global scratch slot.
    public static void SetCurrentGameWindow(int windowPtr)
    {
        GameWindowGlobals.GlobalScratchHandle = windowPtr;
    }

    // FUN_1007b018 — set the sprite-animation loop config on the render context
    // (ctx+0x5e/0x60/0x62/0x64).
    public static void SetSpriteLoopConfig(byte loopEnabled, short loopStart, short loopEnd, short loopValue)
    {
        GlobalState.SpriteLoopEnabled = loopEnabled;
        GlobalState.SpriteLoopStart = loopStart;
        GlobalState.SpriteLoopEnd = loopEnd;
        GlobalState.SpriteLoopValue = loopValue;
    }

    // FUN_1007aed8 — paint the four black letterbox borders around the centred inner play area,
    // then blit the inner region from the offscreen GWorld (window+0x1e) to the window port.
    // Window-record fields: port = *window; portRect at port+0x10/0x12/0x14/0x16
    // (top/left/bottom/right); window+0x1a = inner-right, window+0x1c = inner-bottom (the
    // centred play-area extent).
    public static void PaintLetterboxAndBlitInner()
    {
        int port = GlobalState.ActivePortPixmap;        // *_DAT_10080d08 (ctx+0)

        short portTop, portLeft, portBottom, portRight;
        if (port == 0x1008f720 || port == 0x1008f724)
        {
            // Screen/game pixmap SENTINEL keys: no real CGrafPort record exists behind them, so
            // +0x10..0x16 are NOT a portRect. The window port rect the original reads here is
            // cached in the render context.
            portTop = GlobalState.PortTop;     // ctx+0x0c (cache of window portRect +0x10)
            portLeft = GlobalState.PortLeft;    // ctx+0x0e
            portBottom = GlobalState.PortBottom;  // ctx+0x10
            portRight = GlobalState.PortRight;   // ctx+0x12
        }
        else
        {
            // A real window record: read its embedded CGrafPort portRect through the toolbox
            // accessor (the raw window-record read is the Toolbox's documented boundary).
            MacToolbox.GetPortRect(port, out int portTlPacked, out int portBrPacked);
            portTop = (short)(portTlPacked >> 16);
            portLeft = (short)portTlPacked;
            portBottom = (short)(portBrPacked >> 16);
            portRight = (short)portBrPacked;
        }
        short innerRight = GlobalState.InnerRight;      // ctx+0x1a
        short innerBottom = GlobalState.InnerBottom;     // ctx+0x1c

        Graphics.SaveCurrentPortAndDevice.Run(out int savedPort, out int savedDevice);
        Graphics.SetGamePortAndDevice.Run();

        // The four borders outside the inner play area (SetRect args: left,top,right,bottom).
        PaintRectLTRB(portLeft, portTop, 0, portBottom);
        PaintRectLTRB(0, portTop, innerRight, 0);
        PaintRectLTRB(0, innerBottom, portRight, portBottom);
        PaintRectLTRB(innerRight, portTop, portRight, innerBottom);

        // Blit the inner [0,0,innerRight,innerBottom] from offscreen (window+0x1e) to port.
        var innerRect = new short[] { 0, 0, innerBottom, innerRight };
        MacToolbox.CopyBits(GlobalState.OffscreenGameGWorld + 2, port + 2, innerRect, innerRect, 0, 0);

        SetPortAndDevice.Run(savedPort, savedDevice);
    }

    private static void PaintRectLTRB(short left, short top, short right, short bottom)
    {
        MacToolbox.PaintRect(new[] { top, left, bottom, right });
    }

    // FUN_10052224 — show + select the game window, paint the letterbox, hide the menu bar, and
    // cache the play-area scale factors. The decompile's a5/RTOC base is GameToc: +0x6d61 =
    // WorldState.MenuBarHidden, +0x7b9e/+0x7ba0 = camera-centre cells (WorldFlags.CameraCentreX/Y),
    // -0x6508 = the 0.5 scale double (0x10082158), -0x6510 = the i2d bias (0x10082150).
    public static void ShowGameWindow()
    {
        MacToolbox.ShowWindow(GlobalState.ActivePortPixmap);   // *_DAT_10080d08 (ctx+0)
        MacToolbox.SelectWindow(GlobalState.ActivePortPixmap);
        PaintLetterboxAndBlitInner();
        Dialog.HideMacMenuBar.Run();
        WorldState.MenuBarHidden = 1;
        Graphics.SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect); // window portRect (ctx+0xc)
        // centre = 0.5 * (int)extent — the decompile's float cast idiom is the exact signed
        // int->double cast, collapsed here to (double).
        const double CentreScale = 0.5;
        Core.Model.WorldFlags.CameraCentreX =
            (short)(int)(CentreScale * (double)((GlobalState.PortRight - GlobalState.PortLeft) - 144)); // -0x90 status panel
        Core.Model.WorldFlags.CameraCentreY =
            (short)(int)(CentreScale * (double)(GlobalState.PortBottom - GlobalState.PortTop));
    }

    // FUN_10044048 — toggle the game window between foreground (show + grab input) and
    // background (hide + restore menu bar). The a5+0x6d61 write is the menu-bar-hidden byte
    // (WorldState.MenuBarHidden; a5 base = GameToc).
    public static void SetGameWindowForeground(bool foreground)
    {
        Title.Model.TitleScreenGlobals.InBackground = !foreground;   // *_DAT_100810fc (write through the ptr)
        if (!foreground)
        {
            Dialog.RestoreMacMenuBar.Run();
            Palette.ReapplySaved();
            MacToolbox.HideWindow(GlobalState.ActivePortPixmap);   // *_DAT_10080d08 (ctx+0)
        }
        else
        {
            // Hardware volume = master-volume pref (GamePrefs.MasterVolume) << 5.
            Sound.SetMasterVolume.Run((ushort)((int)GamePrefs.MasterVolume << 5));
            Graphics.ValidateAndResyncDisplay.Run();
            MacToolbox.SetCursor(0);   // qd.arrow — SetCursor is a no-op shim
            Graphics.SetGamePortAndDevice.Run();
            Dialog.RestoreMacMenuBar.Run();
            WorldState.MenuBarHidden = 0;
            MacToolbox.ShowWindow(GlobalState.ActivePortPixmap);     // *_DAT_10080d08 (ctx+0)
            MacToolbox.SelectWindow(GlobalState.ActivePortPixmap);
            Graphics.SetGamePortAndDevice.Run();
            MacToolbox.InvalRect(GlobalState.PortRect); // window portRect (ctx+0xc)
        }
    }

}
