using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_1003a3e4 (EV Override-11.c lines 23865-23899). Preloads the
// outfitter grid's backdrop + one icon per outfit slot into the icon-strip
// GWorld; its only caller is AdvanceLoadout. Loops OutfitTable.Count (128)
// icons — don't confuse with the shipyard's sibling PreloadShipyardIconStrip
// (FUN_1003c4d4), which loops ShipClassTable.Count (64) and is called only
// from RunShipyardDialog.
public static class PreloadOutfitIconStrip
{
    public static void Run()
    {
        GWorldPort.SetActivePortScratch();
        int backdropPicture = MacToolbox.GetPicture(0x17d4);   // PICT 6100
        if (backdropPicture != 0)
        {
            short[] backdropRect = new short[4];
            MacToolbox.SetRect(backdropRect, 0, 0, (short)(MacToolbox.ReadResourceShort(backdropPicture, 8) - MacToolbox.ReadResourceShort(backdropPicture, 4)),
                        (short)(MacToolbox.ReadResourceShort(backdropPicture, 6) - MacToolbox.ReadResourceShort(backdropPicture, 2)));
            MacToolbox.DrawPicture(backdropPicture, backdropRect);
            MacToolbox.HPurge(backdropPicture);
            MacToolbox.ReleaseResource(backdropPicture);
        }
        for (int iconIndex = 0; iconIndex < OutfitTable.Count; iconIndex++)
        {
            int iconPicture = MacToolbox.GetPicture(iconIndex + 0x17d5);   // PICT 6101 + i
            if (iconPicture != 0)
            {
                MacToolbox.DrawPicture(iconPicture, GridLayout.IconStripRects[iconIndex]);
                MacToolbox.HPurge(iconPicture);
                MacToolbox.ReleaseResource(iconPicture);
            }
        }
        SetGamePortAndDevice.Run();
    }
}
