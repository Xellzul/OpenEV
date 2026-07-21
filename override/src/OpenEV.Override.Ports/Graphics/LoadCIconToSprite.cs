using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10060f28 (EV Override-11.c lines 40540-40584): load colour icon 'cicn'
// id `iconId` and wrap it in a managed sprite-frame record. Returns the sprite, or 0
// when the icon is missing. Called every boot by LoadSpriteSheetsAndGWorlds
// (docking-ring quads, streak frames, HUD orbs, target brackets, cicn 20000).
//
// Host bridge: GetCIcon decodes the cicn (OpenEV.Platform.Imaging.CicnDecoder, mask folded
// into alpha) and registers it as a scratch pixmap; the sprite-frame record's ColorRef
// holds that pixmap key so the node CopyMask blit resolves it — the same
// ColorRef-as-pixmap-key path TitleAdapter.BuildShipSpriteTable uses. (The Mac built
// a sprite GWorld and plotted the cicn into it; the true-colour host samples the
// decoded image directly, so that GWorld dance is unnecessary.)
public static class LoadCIconToSprite
{
    public static int Run(short iconId)
    {
        int ciconHandle = MacToolbox.GetCIcon(iconId);
        if (ciconHandle == 0)
        {
            return 0;
        }
        var img = MacToolbox.ResolveScratchPixmap(ciconHandle);
        if (img is null) return 0;
        var rec = Graphics.Model.SpriteFrames.Register();
        rec.ColorRef = ciconHandle;          // CopyMask srcBits key (the scratch pixmap)
        rec.BoundsTop = 0;                     // cicn bounds OffsetRect'd to origin
        rec.BoundsLeft = 0;
        rec.BoundsBottom = (short)img.Height;
        rec.BoundsRight = (short)img.Width;
        // FUN_1007a070 (EV Override-11.c 52070-52071) LIFO-inserts each cicn frame into the
        // ctx+0xc2 rerender list (GlobalState.SpriteListHead2) so
        // RerenderAllSpritesForCurrentDepth can re-plot it on a pixel-depth change. cicn-only
        // (FUN_1007a070 is reached solely via FUN_1007a138 <- FUN_10060f28), so it lives here,
        // NOT in the shared SpriteFrames.Register() that ship/planet sprites also use.
        rec.NextInList2 = Core.Model.GlobalState.SpriteListHead2;   // new.next = old head
        Core.Model.GlobalState.SpriteListHead2 = rec.Handle;        // head = new
        return rec.Handle;
    }
}
