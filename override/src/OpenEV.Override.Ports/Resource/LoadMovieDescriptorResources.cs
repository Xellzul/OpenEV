using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads the 'dëqt' QuickTime-movie descriptor resources (by INDEX, not id) into the
// typed managed MovieTable.Store. 'dëqt' is NOT a cargo type; commodities are 'jünk'.
// The consumer is PlayMovieById → PlayQuickTimeMovie, which opens movie.Name as a .mov file.
//
// NOTE: base EVO 1.0.2 contains ZERO 'dëqt' resources, so CountResources returns 0 and
// the table stays empty (all MovieId = -1). PlayMovieById therefore never matches, and
// every intro/mission "movie" degrades to its dësc text + PICT fallback — exactly as the
// shipping game did.
public static class LoadMovieDescriptorResources
{
    public static void Run()
    {
        for (int i = 0; i < MovieTable.Count; i++)
        {
            GameData.Movies[i].Flags = 0;
            GameData.Movies[i].MovieId = -1;
        }
        short resCount = (short)MacToolbox.CountResources(MacResType.Movie);
        for (int loopIdx = 0; loopIdx < resCount && loopIdx < MovieTable.Count; loopIdx++)
        {
            int resHandle = MacToolbox.GetIndResource(MacResType.Movie, loopIdx + 1);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                var movie = GameData.Movies[loopIdx];
                movie.Flags = MacToolbox.ReadResourceShort(resHandle, 0);
                string name = MacToolbox.GetResInfo(resHandle);
                movie.Name = name.Length > 0x3e ? name.Substring(0, 0x3e) : name;   // FUN_10076178 copies a 0x3f-byte Pascal buffer (byte 0 = length prefix), so only 0x3e (62) chars are real
                // MovieId = GetResInfo's &idOut (the resource's id) — don't revert to resHandle-order/index.
                movie.MovieId = MacToolbox.GetResId(resHandle);
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
