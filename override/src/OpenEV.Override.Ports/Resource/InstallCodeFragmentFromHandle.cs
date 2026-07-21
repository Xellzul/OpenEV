using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_1007b038 (EV Override-11.c lines 52672-52700): install a 'PRbl' sprite-
// blitter code fragment from a resource handle and return the fragment ptr
// (the GlobalState.SpriteBlitterFrags[] tokens), falling back to the built-in
// renderer TVector on a missing resource or a failed CFM load.
public static class InstallCodeFragmentFromHandle
{
    public static int Install(int codeHandle)
    {
        if (codeHandle == 0)
            return ResourceGlobals.SpriteRendererVariant;
        MacToolbox.MoveHHi(codeHandle);
        MacToolbox.HLock(codeHandle);
        int size = MacToolbox.GetHandleSize(codeHandle);
        // DEVIATION (faithful): GetMemFragment (CFM) is a 0-returning params-stub on
        // Windows (no real Code Fragment Manager) — it discards every argument, so two
        // further gaps below are currently inert:
        //   - The decompile passes the Handle's DEREFERENCED master pointer (*param_1,
        //     the locked data's address) as GetMemFragment's memAddr arg; this passes
        //     the handle token (codeHandle) itself, undereffed.
        //   - `5` is the trap's CFragLoadOptions/findFlags argument (the decompile also
        //     just has the bare literal `5` — the decompile didn't recover the enum type); its
        //     exact bit semantics aren't pinned from any local reference.
        // The decompile's success path also returns the connection out-slot the trap
        // filled; the stub fills nothing, so the installed-fragment token here is 0. A
        // REAL failed load would return the fallback SpriteRendererVariant instead — but
        // GlobalState.SpriteBlitterFrags[] is only read by SelectSpriteRenderersByDepth
        // for RenderMode in {1,2,4,8,16,32}, and the host pins RenderMode=0, so the array
        // (and thus token 0 vs. the fallback) is never actually read in the port. Revisit all
        // of this together if the blitter family or a real CFM load is ever implemented.
        short err = (short)MacToolbox.GetMemFragment(codeHandle, size, ResourceGlobals.FragmentNameA, 5);
        return err != 0 ? ResourceGlobals.SpriteRendererVariant : 0;
    }
}
