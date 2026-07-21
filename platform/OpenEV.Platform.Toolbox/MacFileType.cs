namespace OpenEV.Platform.Toolbox;

// File-level Mac OSTypes the game stamps on documents it CREATES via FSpCreateResFile
// (creator signature + file type). Separate from MacResType, which is for resource
// OSTypes loaded via GetResource. 4-byte big-endian; the name decodes the bytes
// (MacRoman: 0x8d = ç).
public static class MacFileType
{
    // EV Override's application creator signature 'EsçO' (0x45738d4f). Stamped on every
    // document the game creates — the prefs file (WritePrefsToDisk) and each pilot save
    // (SavePilotFile) — so the Finder ties them back to the app.
    public const int EvoCreator = 0x45738d4f;

    // The pilot save-file's TYPE is 'OpïL' = MacResType.PilotRecord (the same 4CC also
    // serves as the record's resource type inside the file), so it has no separate entry
    // here. The prefs file's type 'Op¨Ä' is prefs-local (Dialog.PrefsFile.FileType).
}
