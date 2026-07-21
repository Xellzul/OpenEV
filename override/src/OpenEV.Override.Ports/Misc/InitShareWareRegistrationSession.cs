using OpenEV.Override.Ports.Pilot;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007186c (EV Override-11.c lines 46647-46697) — open the
// Ambrosia shareware-registration session: pull the owner name / reg code
// from STR# 900 (items 1/4), stamp the session-start time, load-or-init the
// 0x11c stats record from the prefs file, and mark the session open.
//
// All state lives in the managed ShareWareGlobals. The session OPENS on a stock EVO install:
// STR# 900 items 1/4 exist
// (owner/code = "EV Override"), and with the File Manager substrate (MacToolbox.HfsDataFork)
// active, LoadOrInitPilotPrefsRecord load-or-inits the 0x11c record and returns 0 → Registered
// becomes 1. (Returns -1200 only on a build truly missing STR# 900 items 1/4; -43 only if the
// prefs record can't be created on disk.)
public static class InitShareWareRegistrationSession
{
    public static int Run(byte initDate, ushort userMode)
    {
        int result;

        if (ShareWareGlobals.Registered == 0)
        {
            // Clamp the requested user mode into [1, 2].
            EvoGlobals.ShareWareUserMode = userMode;
            if (EvoGlobals.ShareWareUserMode == 0)
            {
                EvoGlobals.ShareWareUserMode = 1;
            }
            if (EvoGlobals.ShareWareUserMode > 2)
            {
                EvoGlobals.ShareWareUserMode = 2;
            }

            ShareWareGlobals.OwnerName = MacToolbox.GetIndString(900, 1);
            if (ShareWareGlobals.OwnerName.Length == 0)
            {
                result = -1200; // 0xfffffb50 — missing owner-name reg string
            }
            else
            {
                ShareWareGlobals.RegCode = MacToolbox.GetIndString(900, 4);
                if (ShareWareGlobals.RegCode.Length == 0)
                {
                    result = -1200; // 0xfffffb50 — missing reg-code string
                }
                else
                {
                    ShareWareGlobals.RegDateApplied = initDate;
                    if (initDate != 0)
                    {
                        MacToolbox.GetDateTime(out ShareWareGlobals.SessionStartSeconds);
                    }
                    result = LoadOrInitPilotPrefsRecord.Run();
                    if ((short)result == 0)
                    {
                        ShareWareGlobals.Registered = 1;
                    }
                }
            }
        }
        else
        {
            result = -1001; // 0xfffffc17 — session already registered/open
        }
        return result;
    }
}
