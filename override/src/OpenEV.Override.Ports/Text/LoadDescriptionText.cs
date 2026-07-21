using OpenEV.Platform.Toolbox;
using System;

namespace OpenEV.Override.Ports.Text;

// Port of FUN_100197d8 (EV Override-11.c 11568-11591): loads the 'dësc' resource by id and
// returns its text; a missing resource returns the toc-27000 data-seg default (empty string).
public static class LoadDescriptionText
{
    // GetResource returns a MANAGED registry handle (an int token, not a real pointer):
    // read the bytes via ResourceBytes(handle), never ReadInt(handle) (which derefs address 0).
    public static string Load(short descId)
    {
        int descHandle = MacToolbox.GetResource(MacResType.Desc, descId);
        if (descHandle == 0)
        {
            return "";   // data-seg default at toc-27000 (dumped: empty)
        }
        MacToolbox.HNoPurge(descHandle);
        byte[]? bytes = MacToolbox.ResourceBytes(descHandle);
        // The 'dësc' payload is a NUL-terminated C string (FUN_1007615c strcpy's up to the NUL).
        // Decode the pre-NUL bytes as Mac-Roman (code page 10000) — NOT (char)b, which is a Latin-1
        // cast that mangled the high bytes: the curly apostrophe 0xD5 became U+00D5 'Õ', so
        // "ship's" rendered as "shipÕs". MacBitmapFont keys glyphs by the Unicode a Mac-Roman byte
        // maps to, so the string must be true Unicode.
        string text = "";
        if (bytes is not null)
        {
            int len = Array.IndexOf(bytes, (byte)0);
            if (len < 0) len = bytes.Length;
            text = MacToolbox.MacRomanToString(bytes, 0, len);
        }
        MacToolbox.HPurge(descHandle);
        MacToolbox.ReleaseResource(descHandle);
        return text;
    }

    // ~24 call sites stage the loaded text into managed scratch fields (TextScratch.Text,
    // OutfitDescText.Text, …) because mission-token expansion (SubstituteMissionDescTags)
    // rewrites it in place there before TETextBox/news read it — that staging used to run
    // through EvoMemory buffers before the EvoMemory-removal migration. New code: call Load().
}
