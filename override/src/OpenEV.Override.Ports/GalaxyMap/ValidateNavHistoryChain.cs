using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_10034b04 (EV Override-11.c lines 21581-21603)
// On arrival: if the next route stop ([1]) is the system the player is now in,
// retire the head and compact; if the route is empty past the head, clear it all.
public static class ValidateNavHistoryChain
{
    public static void Run()
    {
        var nav = GalaxyMapGlobals.NavHistory;
        if (nav[1] == GameData.Player.CurrentSystem)
        {
            nav[0] = -1;
            CompactNavHistoryHead.Run();
        }
        else if (nav[1] == -1)
        {
            for (int slotIndex = 0; slotIndex < GalaxyMapGlobals.NavHistoryLength; slotIndex++)
            {
                nav[slotIndex] = -1;
            }
        }
    }
}
