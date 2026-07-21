using OpenEV.Override.Ports.Boot;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Pilot;
using OpenEV.Override.Ports.Systems;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_1001b410 (EV Override-11.c lines 12317-12369).
// Title screen "Open Pilot" button: picks and loads a pilot save file.
public static class OpenPilot
{
    // Always returns 0/1 (a Rule 18 bool-return candidate), but its callers
    // (DispatchTitleEvent.cs) are out of scope for this pass
    // — left as int; convert together with those call sites in a later pass.
    public static int Run()
    {
        int[] osTypeFilter = { (int)MacResType.PilotRecord };
        bool sfGood = MacToolbox.StandardGetFile(osTypeFilter, out string pilotLeafName);
        int result;
        if (!sfGood)
        {
            MacToolbox.SetCursor(0);   // qd.arrow cursor — no-op shim
            result = 0;
        }
        else
        {
            CleanupSystNpcs.Run(1);
            InitGameWorldState.Run(1);
            ResetCommodityPriceLimits.Run(1);
            CleanupSystNpcs.Run(1);
            short loaderResult = (short)LoadPluginPilotData.Run(0, 0, pilotLeafName);
            MacToolbox.SetCursor(0);   // qd.arrow cursor — no-op shim
            if (loaderResult == -45)   // old pilot-file format
            {
                loaderResult = (short)LoadPilotPluginFile.Run(0, 0, pilotLeafName);
                if (loaderResult == 0)
                {
                    // "reccomend" is a typo in the original game text — keep it verbatim.
                    AlertModal_OneButton.Run(
                        "This old pilot file was imported successfully. We reccomend you start a new pilot " +
                        "to use with this version of EV, so don’t be surprised if things act differently.");
                }
                else
                {
                    AlertModal_OneButton.Run("An error occured while trying to import this old pilot file!");
                }
            }
            if (loaderResult == 0)
            {
                // Success writes only DeathTimer/WorldCountdown and returns 1 — it does NOT set
                // the pilot name here. The inner loaders above already set it (capped to 31 chars);
                // writing the raw, uncapped picked filename here could overwrite that.
                GameData.Ships[0].DeathTimer = -1.0f;
                WorldState.WorldCountdown = -1;
                result = 1;
            }
            else
            {
                if (loaderResult == -42)   // wrong EV version
                {
                    AlertModal_OneButton.Run(
                        "This pilot file was created with a different version of EV, and can’t be used.");
                }
                result = 0;
            }
        }
        return result;
    }
}
