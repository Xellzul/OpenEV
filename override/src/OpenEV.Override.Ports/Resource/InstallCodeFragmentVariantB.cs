using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_1007b0f0 (EV Override-11.c lines 52701-52729): install a 'PMbl' sprite-
// blitter code fragment from a resource handle and return the fragment ptr,
// falling back to the built-in renderer TVector on a missing resource or a
// failed CFM load. (The B variant of InstallCodeFragmentFromHandle.)
public static class InstallCodeFragmentVariantB
{
    public static int Install(int codeHandle)
    {
        if (codeHandle == 0)
            return ResourceGlobals.DefaultSpriteRenderer;
        MacToolbox.MoveHHi(codeHandle);
        MacToolbox.HLock(codeHandle);
        int size = MacToolbox.GetHandleSize(codeHandle);
        // DEVIATION (faithful): GetMemFragment is a 0-returning params-stub — see
        // InstallCodeFragmentFromHandle for the full note (the *codeHandle-deref gap,
        // the unpinned findFlags=5 semantics, and why token 0 vs. the fallback is never
        // actually observed since SpriteBlitterFrags[] is only read for RenderMode != 0).
        short err = (short)MacToolbox.GetMemFragment(codeHandle, size, ResourceGlobals.FragmentNameB, 5);
        return err != 0 ? ResourceGlobals.DefaultSpriteRenderer : 0;
    }
}
