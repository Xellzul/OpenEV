using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_1003c4d4 (EV Override-11.c lines 24718-24752). Preloads the
// shipyard grid's backdrop + one icon per ship class into the icon-strip GWorld;
// its only caller is RunShipyardDialog. Loops ShipClassTable.Count (64) icons —
// don't confuse with the outfitter's sibling PreloadOutfitIconStrip (FUN_1003a3e4),
// which loops OutfitTable.Count (128) and is called only from AdvanceLoadout.
public static class PreloadShipyardIconStrip
{
    public static void Run()
    {
        GWorldPort.SetActivePortScratch();
        int backdropPic = MacToolbox.GetPicture(0x13ec);   // PICT 5100
        if (backdropPic != 0)
        {
            short[] backdropRect = new short[4];
            MacToolbox.SetRect(backdropRect, 0, 0, (short)(MacToolbox.ReadResourceShort(backdropPic, 8) - MacToolbox.ReadResourceShort(backdropPic, 4)),
                        (short)(MacToolbox.ReadResourceShort(backdropPic, 6) - MacToolbox.ReadResourceShort(backdropPic, 2)));
            MacToolbox.DrawPicture(backdropPic, backdropRect);
            MacToolbox.HPurge(backdropPic);
            MacToolbox.ReleaseResource(backdropPic);
        }
        for (int iconIndex = 0; iconIndex < ShipClassTable.Count; iconIndex++)
        {
            int iconPic = MacToolbox.GetPicture(iconIndex + 0x13ed);   // PICT 5101 + i
            if (iconPic != 0)
            {
                MacToolbox.DrawPicture(iconPic, GridLayout.IconStripRects[iconIndex]);
                MacToolbox.HPurge(iconPic);
                MacToolbox.ReleaseResource(iconPic);
            }
        }
        SetGamePortAndDevice.Run();
    }
}
