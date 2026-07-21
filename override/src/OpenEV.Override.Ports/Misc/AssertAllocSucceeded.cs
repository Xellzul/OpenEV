using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10078bac — EV Override-11.c lines 51308-51321.
public static class AssertAllocSucceeded
{
    public static void Run(int ptr)
    {
        if (ptr == 0)
        {
            // Message from data-seg cell 0x1008594c (StaticData.UiErrorStrings[OutOfMemoryMessageIndex]).
            FatalOutOfMemoryExit.Run(StaticData.UiErrorStrings[StaticData.OutOfMemoryMessageIndex]);
        }
    }
}
