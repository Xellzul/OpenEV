using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_10034aa4 (EV Override-11.c lines 21561-21580)
// When the route head has been consumed (slot 0 == -1), shift the whole
// nav-history list down one and re-terminate the tail.
public static class CompactNavHistoryHead
{
    public static void Run()
    {
        var nav = GalaxyMapGlobals.NavHistory;
        if (nav[0] == -1)
        {
            for (int slotIndex = 0; slotIndex < GalaxyMapGlobals.NavHistoryLength - 1; slotIndex++)
            {
                nav[slotIndex] = nav[slotIndex + 1];
            }
            nav[GalaxyMapGlobals.NavHistoryLength - 1] = -1;
        }
    }
}
