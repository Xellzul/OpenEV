using System.Collections.Generic;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.EvoData.Resources;

// Mac DITL (Dialog Item List). Per Inside Macintosh: Macintosh Toolbox Essentials,
// ch. 6, table "DITL data". Layout:
//   itemCount-1 : int16
//   For each item:
//     placeholder : 4 bytes (always 0 in resource)
//     bounds      : Rect (top,left,bottom,right; 8 bytes, signed int16 each)
//     itemType    : 1 byte — high bit = disabled flag, low 7 bits = type code
//                   types: 0=user, 4=button, 5=checkbox, 6=radio, 7=control(CNTL),
//                          8=static text, 16=editable text, 32=icon (ICON), 64=PICT
//     data        : 1-byte length-prefixed payload, padded to even byte count
//                   For buttons/static text/edit text: that payload IS a Mac Pascal
//                   string (the length byte we just read is the string length).
//                   For 7/32/64: payload is a 2-byte resource id (so len byte = 2).
public enum DitlItemKind : byte
{
    UserItem = 0,
    Button = 4,
    Checkbox = 5,
    RadioButton = 6,
    Control = 7,
    StaticText = 8,
    EditableText = 16,
    Icon = 32,
    Picture = 64,
}

public sealed record RawDitlItem(
    short Top, short Left, short Bottom, short Right,
    DitlItemKind Kind, bool Enabled,
    string Text,
    short ResourceId)
{
    public short Width => (short)(Right - Left);
    public short Height => (short)(Bottom - Top);
}

public sealed record RawDitlRecord(IReadOnlyList<RawDitlItem> Items)
{
    public static RawDitlRecord Load(byte[] data)
    {
        var r = new BigEndianSpanReader(data);
        var items = new List<RawDitlItem>();
        if (r.Remaining < 2) return new RawDitlRecord(items);
        ushort countMinus1 = r.ReadUInt16();
        int count = countMinus1 + 1;

        for (int i = 0; i < count && r.Remaining >= 14; i++)   // 14 = 4 placeholder + 8 rect + 1 typeByte + 1 dataLen
        {
            r.Skip(4); // placeholder handle (always 0 in resource)
            short top = r.ReadInt16();
            short left = r.ReadInt16();
            short bottom = r.ReadInt16();
            short right = r.ReadInt16();
            byte typeByte = r.ReadByte();
            bool enabled = (typeByte & 0x80) == 0; // high bit set = disabled
            var kind = (DitlItemKind)(typeByte & 0x7F);
            byte dataLen = r.ReadByte();
            string text = "";
            short resId = 0;
            if (dataLen > 0 && r.Remaining >= dataLen)
            {
                var payload = r.ReadBytes(dataLen).ToArray();
                if (kind is DitlItemKind.Button or DitlItemKind.Checkbox
                          or DitlItemKind.RadioButton or DitlItemKind.StaticText
                          or DitlItemKind.EditableText)
                {
                    // DITL item text is Mac Roman, not Windows-1252 (unlike the OSType
                    // folding in EvoResourceType, which deliberately wants the cp1252
                    // mis-decode) — this is real UI text, e.g. "Game Speed…"/"…", so it
                    // must round-trip through the correct code page or high-bit
                    // punctuation (ellipsis, dashes, curly quotes) renders as mojibake.
                    text = MacRoman.GetString(payload);
                }
                else if (kind is DitlItemKind.Control or DitlItemKind.Icon or DitlItemKind.Picture
                         && dataLen >= 2)
                {
                    resId = (short)((payload[0] << 8) | payload[1]);
                }
            }
            // payload is padded to even length
            if (dataLen % 2 != 0 && r.Remaining >= 1) r.Skip(1);

            items.Add(new RawDitlItem(top, left, bottom, right, kind, enabled, text, resId));
        }
        return new RawDitlRecord(items);
    }
}
