using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Resource;

// Orchestrator for FUN_10015e70 (EV Override-11.c 10694-11567) — each resource-load loop
// lives in its own Load*Resources.cs file; this dispatches them in original order.
public static class LoadSpobAndStellarResources
{
    public static void Run()
    {
        if (BugBits.IsSet(BugBit.NoOverwriteExistingData)) return;
        LoadSpobResources.Run();
        LoadSystResources.Run();
        LoadOutfitResources.Run();
        LoadShipClassResources.Run();
        LoadWeaponResources.Run();
        LoadDudeResources.Run();
        LoadGovtResources.Run();
        LoadPersResources.Run();
        LoadCronResources.Run();
        LoadNebulaResources.Run();
        LoadFleetResources.Run();
        LoadJunkResources.Run();
        LoadMovieDescriptorResources.Run();   // 'dëqt' QT-movie descriptors (empty in base EVO)
    }
}
