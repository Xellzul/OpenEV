namespace OpenEV.Override.Ports.Pilot.Model;

// The pilot-prefs data file's name string: a fixed Pascal-string constant in the PEF
// data segment at 0x10084f6e (decompile reaches it as &DAT_10084f6e — an address-of, so
// the absolute literal is correct; NOT a toc-relative or heap value). Passed by address
// to HOpen / HCreate / HGetFInfo / HSetFInfo. Shared by WritePilotRecordToPrefsFile and
// LoadOrInitPilotPrefsRecord, hence this one named constant.
public static class PilotPrefsFile
{
    public const int NameStr = 0x10084f6e;
}
