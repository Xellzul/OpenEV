using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10073b5c (EV Override-11.c lines 47946-47981).
//
// Currently unreferenced: the sole original caller, FUN_10074930 (the Register-app
// catalog search), is kept a no-op — see DEV_DEBUG_CODE.md DDC-04.
public static class BestEffortAllocate
{
    public static void Run(uint requestedSize, out uint grantedSize)
    {
        // 0xfffffc00 = round size down to a 1024-byte (1 KB) boundary.
        requestedSize = requestedSize & 0xfffffc00;
        if ((int)requestedSize < 1024)
        {
            requestedSize = 1024;
        }
        int ptr = MacToolbox.NewPtr((int)requestedSize);
        if (ptr == 0 && 1024 < (int)requestedSize)
        {
            ptr = MacToolbox.FreeMem();
            // int - uint promotes to long in C#; the (uint) cast restores the C semantics
            // of the decompile's `iVar2 - 0x8000U`, which is unsigned (reserve 32 KB headroom).
            uint adjFree = (uint)(ptr - 32768U) & 0xfffffc00;
            uint maxBlock = (uint)MacToolbox.MaxBlock();
            requestedSize = maxBlock & 0xfffffc00;
            if ((int)adjFree < (int)requestedSize)
            {
                requestedSize = adjFree;
            }
            if (requestedSize == 0)
            {
                requestedSize = 1024;
            }
            ptr = MacToolbox.NewPtr((int)requestedSize);
        }
        if (ptr == 0)
        {
            grantedSize = 0;
        }
        else
        {
            grantedSize = requestedSize;
        }
    }
}
