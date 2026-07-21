using OpenEV.Override.Ports.Pilot;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007197c (EV Override-11.c lines 46698-46729).

public static class CloseShareWareRegistrationSession
{
    public static void Run()
    {
        if (ShareWareGlobals.Registered != 0)
        {
            if (ShareWareGlobals.NotificationPending != 0)
            {
                RemoveNotificationIfDone.Run();
            }
            if (ShareWareGlobals.RegDateApplied != 0)
            {
                ShareWareGlobals.Record.RegCodeWord = ShareWareGlobals.Record.RegCodeWord + 1;
            }
            if (ShareWareGlobals.RegDateApplied != 0 && ShareWareGlobals.SessionStartSeconds != 0)
            {
                MacToolbox.GetDateTime(out int nowSeconds);
                ShareWareGlobals.Record.TotalPlaySeconds =
                    ShareWareGlobals.Record.TotalPlaySeconds + (nowSeconds - ShareWareGlobals.SessionStartSeconds);
            }
            WritePilotRecordToPrefsFile.Run();
            ShareWareGlobals.Registered = 0;
        }
    }
}
