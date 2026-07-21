using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007d8bc (EV Override-11.c lines 54187-54360): the per-frame render orchestrator.
// Tick each render node's update UPP + recompute its bounds, depth-sort + run collisions, erase the
// previous frame, draw every node (custom draw UPP or BlitSpriteByDepth), then composite/flush the
// dirty rects and recycle dead/effect nodes. Port/GDevice are saved/restored around the work when the
// render mode requires it. The render list hangs off the render context (+0x78 =
// GlobalState.SpriteListHead); the context's +0 slot holds the in-game window.
public static class UpdateWindowRegionLayout
{
    // Host deviation (see below): the cached play-area black-fill Rect, a managed
    // short[4] {top,left,bottom,right}. This rect has NO decompile counterpart; the whole
    // black-fill is a port invention substituting for the broken ported dirty-rect erase.
    private static short[]? _playAreaRect;

    public static void Run(bool fullRedraw)
    {
        int renderVariant = ResourceGlobals.SpriteRendererVariant;
        // The render context *0x10080d08 -> GlobalState.

        // (The original's first statement syncs the secondary GWorld's pixmap ctSeed from the
        // primary's via a deep GDevice->gdPMap->pmTable walk. The host's GWorld pixmaps are
        // RenderTarget-backed and carry no shared ctSeed — the walk resolved to garbage near
        // address 0 every frame — so the sync is dropped, not emulated.)

        int activeRenderer = fullRedraw ? GlobalState.CurrentDepthRenderer : renderVariant;
        bool saveRestore = activeRenderer == renderVariant ||
                           GlobalState.CurrentDepthRendererPM == ResourceGlobals.DefaultSpriteRenderer;

        // The saved {port, GDevice} pair (SaveCurrentPortAndDevice writes it, SetPortAndDevice restores).
        int savedPort = 0, savedDevice = 0;
        if (saveRestore)
            SaveCurrentPortAndDevice.Run(out savedPort, out savedDevice);

        // Pass 1: tick each node's update UPP and recompute its bounding box.
        for (int node = GlobalState.SpriteListHead; node != 0;)
        {
            var n = SpriteNodes.At(node);
            if (n.UpdateUpp != 0 && n.UpdateUpp != -1)
                InvokeNodeUpdateUpp.Run(node, n.UpdateUpp);
            n.RectTop = (short)(n.ExtentTop + n.PosY);
            n.RectBottom = (short)(n.ExtentBottom + n.PosY);
            n.RectLeft = (short)(n.ExtentLeft + n.PosX);
            n.RectRight = (short)(n.ExtentRight + n.PosX);
            node = n.Next;
        }
        SpriteListBubbleSortPass.Run();
        DispatchCollisionByAxes.Run();
        if (saveRestore)
            GWorldPort.SetActivePortSecondaryGame();

        // Pass 2: erase each node's previous-frame footprint into the backdrop, snapshot its rect.
        for (int node = GlobalState.SpriteListHead; node != 0;)
        {
            var n = SpriteNodes.At(node);
            if (n.DrawRectBottom != 0)
                GlueTrampoline4Args.RunPorts(GlobalState.AnimScratchPort, GlobalState.OffscreenGameGWorld, n, 0x36, GlobalState.CurrentDepthRenderer);
            n.SetInt(0x3e, n.IntAt(0x36));   // prev rect <- draw rect (two-int copy)
            n.SetInt(0x42, n.IntAt(0x3a));
            node = n.Next;
        }
        foreach (var dirtyRect in GlobalState.DirtyRects)
            GlueTrampoline4Args.RunPorts(GlobalState.AnimScratchPort, GlobalState.OffscreenGameGWorld, dirtyRect, GlobalState.CurrentDepthRenderer);

        // Host: clear the play area before DRAW. The ported per-sprite dirty-rect ERASE has broken
        // bounding rects, so substitute a full play-area black fill (portRect minus the 144px panel).
        _playAreaRect ??= new[]
        {
            GlobalState.PortTop, GlobalState.PortLeft,
            GlobalState.PortBottom, (short)(GlobalState.PortRight - 144),
        };
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(_playAreaRect);
        // The backdrop GWorld is the erase SOURCE this fill substitutes for, and it carries the
        // live HUD chatter text (EnqueueChatterEvent draws it there; the scroll band re-blacks it
        // and DispatchPendingChatter repairs it; TickFlashEffectCountdown blacks it on expiry).
        // The per-sprite erases restored backdrop pixels — chatter included — into this compose
        // port, which is how the message sat UNDER the sprites drawn below (stars/ships/planets
        // visible over it). Re-apply the chatter strip so the fill doesn't drop the text from the
        // frame; guard as FUN_1005ffe8 does.
        if (GlobalState.AnimScratchPort != 0 && GlobalState.OffscreenGameGWorld != 0)
            MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.OffscreenGameGWorld + 2,
                GlobalState.HudPlayAreaClipRect, GlobalState.HudPlayAreaClipRect, 0, 0);

        // Pass 3: draw each node's current sprite (custom draw UPP via InvokeSpriteBlitUpp, else BlitSpriteByDepth).
        for (int node = GlobalState.SpriteListHead; node != 0; node = SpriteNodes.At(node).Next)
        {
            var n = SpriteNodes.At(node);
            if (n.UpdateUpp == 0)
                continue;
            int sprite = n.SpritePtr;
            if (sprite == 0)
            {
                n.DrawRectBottom = 0;
                continue;
            }
            var f = SpriteFrames.At(sprite);
            n.SetInt(0x36, n.IntAt(2));   // draw rect top/left <- packed (PosY<<16|PosX)
            n.DrawRectBottom = (short)(n.PosY + f.BoundsBottom);
            n.DrawRectRight = (short)(n.PosX + f.BoundsRight);
            short frameWidth = f.BoundsRight;
            short frameHeight = f.BoundsBottom;
            int customDraw = f.CustomDrawUpp;
            if (customDraw == 0)
            {
                if (n.ClipRgn == 0)
                    InvokeSpriteBlitUpp.Run(sprite, node, GlobalState.OffscreenGameGWorld, 0, n.IntAt(2), frameWidth, frameHeight, GlobalState.CurrentDepthRendererPM);
                else
                    BlitSpriteByDepth.Run(sprite, node, GlobalState.OffscreenGameGWorld, 0, n.IntAt(2), frameWidth, frameHeight);
            }
            else
            {
                InvokeSpriteBlitUpp.Run(sprite, node, GlobalState.OffscreenGameGWorld, 0, n.IntAt(2), frameWidth, frameHeight, customDraw);
            }
        }

        // Optional per-frame callback. If it returns non-zero, recycle the effect list,
        // tick sound, restore, unlink dead nodes, and bail out early.
        if (GlobalState.FrameCallbackUpp != 0)
        {
            if (saveRestore)
                SetPortAndDevice.Run(savedPort, savedDevice);
            if (InvokeUpp1ArgAlt.Run(GlobalState.FrameCallbackUpp) != 0)
            {
                GlobalState.DirtyRects.Clear();
                TickSoundSubsystem.Run();
                if (saveRestore)
                    SetPortAndDevice.Run(savedPort, savedDevice);
                int walk = GlobalState.SpriteListHead;
                while (walk != 0)
                {
                    var n = SpriteNodes.At(walk);
                    walk = n.Next;
                    if (n.UpdateUpp == 0)
                        UnlinkGWorldNode.Run(n.Handle);
                }
                return;
            }
        }
        if (saveRestore)
            SetGamePortAndDevice.Run();

        // Composite pass: clamp each node's dirty rect to the play area, union it with its previous
        // rect, blit, then unlink dead nodes.
        int cur = GlobalState.SpriteListHead;
        while (cur != 0)
        {
            var n = SpriteNodes.At(cur);
            if (n.UpdateUpp != 0 && n.SpritePtr != 0)
            {
                if (n.DrawRectLeft < 0)
                    n.DrawRectLeft = 0;
                if (GlobalState.InnerRight < n.DrawRectRight)
                    n.DrawRectRight = GlobalState.InnerRight;
                if (n.DrawRectTop < 0)
                    n.DrawRectTop = 0;
                if (GlobalState.InnerBottom < n.DrawRectBottom)
                    n.DrawRectBottom = GlobalState.InnerBottom;
                if (n.PrevRectBottom == 0)
                {
                    n.SetInt(0x3e, n.IntAt(0x36));   // prev rect <- draw rect
                    n.SetInt(0x42, n.IntAt(0x3a));
                }
                else
                {
                    // Union the draw rect into the prev rect, per edge.
                    if (n.DrawRectTop < n.PrevRectTop)
                        n.PrevRectTop = n.DrawRectTop;
                    if (n.DrawRectLeft < n.PrevRectLeft)
                        n.PrevRectLeft = n.DrawRectLeft;
                    if (n.PrevRectRight < n.DrawRectRight)
                        n.PrevRectRight = n.DrawRectRight;
                    if (n.PrevRectBottom < n.DrawRectBottom)
                        n.PrevRectBottom = n.DrawRectBottom;
                }
            }
            if (n.PrevRectBottom != 0)
                GlueTrampoline4Args.RunPorts(GlobalState.OffscreenGameGWorld, GlobalState.ActivePortPixmap, n, 0x3e, activeRenderer);
            cur = n.Next;
            if (n.UpdateUpp == 0)
                UnlinkGWorldNode.Run(n.Handle);
        }

        foreach (var dirtyRect in GlobalState.DirtyRects)
            GlueTrampoline4Args.RunPorts(GlobalState.OffscreenGameGWorld, GlobalState.ActivePortPixmap, dirtyRect, activeRenderer);
        GlobalState.DirtyRects.Clear();
        if (saveRestore)
            SetPortAndDevice.Run(savedPort, savedDevice);
        TickSoundSubsystem.Run();
    }
}
