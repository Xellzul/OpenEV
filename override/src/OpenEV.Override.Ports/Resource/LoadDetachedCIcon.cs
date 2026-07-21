using OpenEV.Override.Ports.Misc;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// Port of FUN_10079b40 (EV Override-11.c 51877-51893). Loads colour icon
// 'cicn' id `iconId` and "detaches" it from the resource map by re-fetching
// the raw resource handle and releasing it — the GetCIcon copy survives the
// release. Called from SpriteRerender; MacToolbox.GetCIcon is a real cicn
// decoder (returns 0 only when the resource is missing or undecodable).
public static class LoadDetachedCIcon
{
    public static int Run(int iconId)
    {
        if (ResourceGlobals.ToolboxShimInitFlag == 0)
        {
            InitToolboxShimGlobals.Run();
        }
        int cicon = MacToolbox.GetCIcon(iconId);
        int rawHandle = MacToolbox.GetResource(MacResType.ColorIcon, iconId);
        MacToolbox.ReleaseResource(rawHandle);
        return cicon;
    }
}
