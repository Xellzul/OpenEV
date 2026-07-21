using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1005174c (EV Override-11.c lines 33408-33503).
// Tests whether mission missionIndex's AuxSpawnSystem (+0x5e) encoding selects systIndex.
// matchKind encodes either a sentinel (-1/-2/-3/-6), a direct system (128..1127 => sys-128),
// a system+its hyperlinks (5000..9999), or a government relation test (9999..29999).
public static class MissionSystMatches
{
    public static bool Run(short systIndex, short missionIndex)
    {
        short matchKind = GameData.Missions[missionIndex].AuxSpawnSystem;
        if ((matchKind == -1 || matchKind == -6) && systIndex == GameData.Player.CurrentSystem)
        {
            return true;
        }
        if (matchKind == -2 && GameData.Missions[missionIndex].TargetSpob != -1 &&
            systIndex == GameData.Spobs[GameData.Missions[missionIndex].TargetSpob].System)
        {
            return true;
        }
        if (matchKind == -3 && GameData.Missions[missionIndex].ReturnSpob != -1 &&
            systIndex == GameData.Spobs[GameData.Missions[missionIndex].ReturnSpob].System)
        {
            return true;
        }
        if (matchKind >= 128 && matchKind <= 1127 && systIndex == matchKind - 128)
        {
            return true;
        }

        if (matchKind > 4999 && matchKind < 10000)
        {
            if (systIndex == matchKind - 5000)
            {
                return true;
            }
            for (short i = 0; i < SystRecord.HyperLinkCount; i = (short)(i + 1))
            {
                if (systIndex == SystTable.Store[matchKind - 5000].HyperLink[i])
                {
                    return true;
                }
            }
        }

        short systGovt = SystTable.Store[systIndex].Govt;
        if (matchKind >= 9999 && matchKind <= 14999 && matchKind - 10000 == systGovt)
        {
            return true;
        }

        if (matchKind > 14999 && matchKind < 20000 && systGovt >= 0)
        {
            if (matchKind - 15000 == systGovt)
            {
                return true;
            }
            if (GameData.Governments[systGovt].Ally != -1 && GameData.Governments[matchKind - 15000].Ally != -1)
            {
                if (matchKind - 15000 == GameData.Governments[systGovt].Ally)
                {
                    return true;
                }
                if (GameData.Governments[matchKind - 15000].Ally == systGovt)
                {
                    return true;
                }
            }
        }

        if (matchKind >= 20000 && matchKind <= 24999 && matchKind - 20000 != systGovt)
        {
            return true;
        }

        if (matchKind > 24999 && matchKind < 30000 && systGovt >= 0)
        {
            if ((GameData.Governments[matchKind - 25000].Flags & GovtFlags.Xenophobic) != 0 &&
                matchKind - 25000 != systGovt &&
                matchKind - 25000 != GameData.Governments[systGovt].Ally &&
                GameData.Governments[matchKind - 25000].Ally != systGovt)
            {
                return true;
            }
            if (GameData.Governments[systGovt].Enemy != -1 && GameData.Governments[matchKind - 25000].Enemy != -1)
            {
                if (matchKind - 25000 == GameData.Governments[systGovt].Enemy)
                {
                    return true;
                }
                if (GameData.Governments[matchKind - 25000].Enemy == systGovt)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
