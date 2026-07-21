using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.EvoData.Resources;

// Mac DLOG (Dialog Template). Per Inside Macintosh: Macintosh Toolbox Essentials,
// ch. 6, "Dialog Resources". Layout:
//   bounds      : Rect (8 bytes; top, left, bottom, right)
//   procID      : int16   (window definition / WDEF variant code)
//   visible     : int16   (0 = hidden, 1 = visible at creation)
//   goAwayFlag  : int16   (1 = has close box)
//   refCon      : int32   (application-defined long)
//   itemsID     : int16   (DITL resource ID supplying the controls)
//   title       : PString padded to even byte count
//   (positionType : optional int16 if room remains)
public sealed record RawDlogRecord(
    short Top, short Left, short Bottom, short Right,
    short ProcId, bool Visible, bool GoAway,
    int RefCon, short ItemsId, string Title, short PositionType)
{
    public short Width => (short)(Right - Left);
    public short Height => (short)(Bottom - Top);

    public static RawDlogRecord Load(byte[] data)
    {
        var r = new BigEndianSpanReader(data);
        short top = r.ReadInt16();
        short left = r.ReadInt16();
        short bottom = r.ReadInt16();
        short right = r.ReadInt16();
        short procId = r.ReadInt16();
        short visibleFlag = r.ReadInt16();
        short goAway = r.ReadInt16();
        int refCon = r.ReadInt32();
        short itemsId = r.ReadInt16();

        string title = "";
        if (r.Remaining >= 1)
        {
            byte len = r.ReadByte();
            if (len > r.Remaining) len = (byte)r.Remaining;
            // Same Mac Roman text as DITL item text (RawDitlRecord) — not Windows-1252.
            // Currently unread downstream (DlgTemplate has no Title field), but fixing the
            // decoder here means it's already correct the day a title bar gets wired up.
            title = MacRoman.GetString(r.ReadBytes(len));
            // PString is padded so the next field starts on an even byte.
            int consumed = 1 + len;
            if (consumed % 2 != 0 && r.Remaining >= 1) r.Skip(1);
        }

        short positionType = 0;
        if (r.Remaining >= 2) positionType = r.ReadInt16();

        return new RawDlogRecord(top, left, bottom, right, procId,
            visibleFlag != 0, goAway != 0, refCon, itemsId, title, positionType);
    }
}
