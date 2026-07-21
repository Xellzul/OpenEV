using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;

using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.GalaxyMap;

// Port of FUN_10034088 (EV Override-11.c 21305-21389). Caches the map's 4 nebula
// background PICTs into the anim-scratch port, choosing each PICT's zoom-detail
// column from the live map zoom. The port's DrawGalaxyMap draws these PICTs directly
// instead, so this scratch cache is currently unread — kept for faithfulness.
public static class CacheMapNebulaBackgrounds
{
    // NebulaPicts is a 4-nebula x 3-zoom-detail-column grid (near/mid/far), indexed
    // nebula * ZoomDetailColumns + tier.
    private const int ZoomDetailColumns = 3;

    public static void Run()
    {
        GWorldPort.SetActivePortScratch();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        short prevBottom = 0;
        
        for (int idx = 0; idx < GameData.MapNebulas.Length; idx++)
        {
            var neb = GameData.MapNebulas[idx];
            short[] rect = GalaxyMapState.NebulaScratchRects[idx];
            MacToolbox.SetRect(rect, 0, 0, 0, 0);
            
            if (neb.Charted != 0)
            {
                short scaleTier = 0;
                if (GalaxyMapGlobals.ZoomDetailNearThreshold < GalaxyMapState.Zoom) scaleTier = 0;
                else if (GalaxyMapGlobals.ZoomDetailNearThreshold == GalaxyMapState.Zoom) scaleTier = 1;
                else scaleTier = 2;

                if (GalaxyMapState.NebulaPicts[idx * ZoomDetailColumns + scaleTier] != 0)
                {
                    rect[0] = (short)(GlobalState.ScratchStageTop + prevBottom);
                    rect[1] = GlobalState.ScratchStageLeft;
                    
                    if (GalaxyMapState.Zoom < GalaxyMapGlobals.ZoomDetailFarThreshold)
                    {
                        rect[2] = (short)(int)(rect[0] + neb.Height / GalaxyMapGlobals.ZoomDetailFarThreshold);
                        rect[3] = (short)(int)(rect[1] + neb.Width / GalaxyMapGlobals.ZoomDetailFarThreshold);
                    }
                    else
                    {
                        rect[2] = (short)(int)((float)rect[0] + (float)neb.Height / (float)GalaxyMapState.Zoom);
                        rect[3] = (short)(int)((float)rect[1] + (float)neb.Width / (float)GalaxyMapState.Zoom);
                    }

                    prevBottom = rect[2];
                    MacToolbox.DrawPicture(GalaxyMapState.NebulaPicts[idx * ZoomDetailColumns + scaleTier], rect);
                }
            }
        }
        SetGamePortAndDevice.Run();
    }
}
