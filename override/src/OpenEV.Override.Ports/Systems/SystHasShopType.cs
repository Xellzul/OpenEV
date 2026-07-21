// Port of FUN_1005d840 (EV Override-11.c lines 38793-38837).

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

public static class SystHasShopType
{
    public static bool Run(short systemIndex, short shopType)
    {
        // DEVIATION (faithful): unaff_r28 is an untracked register — the ASM shows it is
        // never initialized on entry (its value is whatever the caller left in r28), so the
        // original value is unrecoverable; this defaults to 0. In practice unreachable: both real
        // call sites (DrawGalaxyMap) only ever pass shopType 0/1/2, so one of the three `if`s
        // below always fires before the mask is read.
        uint shopFlagMask = 0;

        foreach (short spobIdx in SystTable.Store[systemIndex].StellarLink)
        {
            if (spobIdx != -1 && GameData.Spobs[spobIdx].Visible != 0 &&
                (GameData.Spobs[spobIdx].Flags & 0x20) == 0 &&
                (GameData.Spobs[spobIdx].Flags & 0x1) != 0)
            {
                if (shopType == 0) shopFlagMask = 0x2;
                if (shopType == 1) shopFlagMask = 0x4;
                if (shopType == 2) shopFlagMask = 0x8;

                if ((shopFlagMask & (uint)GameData.Spobs[spobIdx].Flags) != 0)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
