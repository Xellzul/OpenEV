using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10078be0 — EV Override-11.c lines 51322-51338.
public static class CheckedAllocClear
{
    public static int Run(int size)
    {
        int freeMem = MacToolbox.FreeMem();
        if (freeMem < size + 2000)
        {
            AssertAllocSucceeded.Run(0);
        }
        int ptr = MacToolbox.NewPtrClear(size);
        AssertAllocSucceeded.Run(ptr);
        return ptr;
    }
}
