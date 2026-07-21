namespace OpenEV.Override.Ports.Core.Model;

// The shared dësc/text scratch — was the 0x400-byte BSS C-string buffer behind
// the PEF-relocated ptr cell 0x10081144 (-> 0x10100fe4). Producers:
// LoadDescriptionText, the mission-name staging, the pers hail line; the tag
// substitution pass (Mission.SubstituteMissionDescTags) rewrites it in place;
// consumers: mission-dialog TETextBox displays, BBS list rows, AlertText
// publishing, pers speech/chatter.
public static class TextScratch
{
    public static string Text = "";

    // The strncpy-style truncation the buffer flows used (copy at most max chars).
    public static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;
}

// The outfitter/shipyard description text — was the C-string buffer behind the
// PEF-relocated ptr cell 0x10081020 (`*(toc-0x7640)`; the old "OutfitGridExtent"
// name for this slot was a MISNAME). Writers: the shop/shipyard filters
// (LoadDescriptionText fill, NUL clear on deselect); readers: the item-6 desc
// TETextBox in DrawOutfitShop / RedrawShipyardDialog.
public static class OutfitDescText
{
    public static string Text = "";
}
