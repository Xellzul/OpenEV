using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 54612-54629.
// Name source: inferred from behavior — 4-arg CFM glue trampoline forwarding 3 args to FUN_1008062c
public static class GlueTrampoline4Args
{
    // DEVIATION (faithful): FUN_1007e3a0 forwards 3 of its 4 args to the CFM glue thunk
    // FUN_1008062c, which indirect-calls the per-depth UPP in window+0xf8 — for the
    // in-game compositor (UpdateWindowRegionLayout) that UPP is FUN_10077968 ==
    // CopyBitsBetweenGWorlds (restore/blit backdrop↔offscreen↔screen):
    // CopyBits(*src+2 → *dst+2 over the rect). The src/dst here are GWorld PORT
    // VALUES (the record fields live in Core.Model.GlobalState — no address to
    // deref), so FUN_10077968's record-deref collapses into the `+ 2` key form.
    // The renderer/depth arg is ignored exactly as FUN_10077968 ignores it — keep the
    // parameter anyway, it documents the ASM's real 4th argument. This collapse is only
    // valid while RenderMode stays pinned to 0 (host substrate; see
    // InvokeSpriteBlitUpp.cs and SelectSpriteRenderersByDepth.cs for the pinning
    // rationale) — the guard below is the same tripwire pattern as the sibling
    // depth-dispatch files (BlitSpriteByDepth, DispatchSpriteByDepth, etc.).

    // Managed-rect overload (dirty-rect list entries and other C# rects).
    public static void RunPorts(int srcPort, int dstPort, short[] rect, int rendererUpp)
    {
        if (GlobalState.RenderMode != 0)
        {
            throw new System.NotSupportedException(
                "GlueTrampoline4Args: RenderMode != 0 (RenderMode pinning changed?) — the renderer/depth " +
                "arg can no longer be assumed to resolve to CopyBitsBetweenGWorlds; re-derive the UPP dispatch.");
        }
        MacToolbox.CopyBits(srcPort + 2, dstPort + 2, rect, rect, 0, 0);
    }

    // Managed-node overload: the rect lives inside the node's byte[].
    public static void RunPorts(int srcPort, int dstPort, SpriteNode node, int rectOff, int rendererUpp)
    {
        var rect = new[] { node.ShortAt(rectOff), node.ShortAt(rectOff + 2),
                           node.ShortAt(rectOff + 4), node.ShortAt(rectOff + 6) };
        RunPorts(srcPort, dstPort, rect, rendererUpp);
    }
}
