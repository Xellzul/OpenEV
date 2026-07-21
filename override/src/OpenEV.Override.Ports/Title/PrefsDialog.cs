namespace OpenEV.Override.Ports.Title;

// Port of FUN_1004445c (EV Override-11.c lines 28360-28365) — a trivial
// wrapper around FUN_10044480 (PrefsDialogInit), the Set Prefs dialog
// (DLOG 4001). DispatchTitleEvent calls this on button 3 (Set Prefs).
public static class PrefsDialog
{
    public static void Run() => PrefsDialogInit.Run();
}
