using System;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

// QuickDraw PICT v2 opcode lengths (Inside Macintosh: Imaging With QuickDraw, App. A).
internal static class PictOpcodeTable
{
    public static int? Skip(ushort opcode, ref BigEndianSpanReader reader)
    {
        switch (opcode)
        {
            case 0x0000: return 0;
            case 0x0001:
                if (reader.Remaining < 2) return null;
                return Math.Max(0, reader.ReadUInt16() - 2);
            case 0x0002: return 8;
            case 0x0003: return 2;
            case 0x0004: return 1;
            case 0x0005: return 2;
            case 0x0006: return 4;
            case 0x0007: return 4;
            case 0x0008: return 2;
            case 0x0009: return 8;
            case 0x000A: return 8;
            case 0x000B: return 4;
            case 0x000C: return 4;
            case 0x000D: return 2;
            case 0x000E: return 4;
            case 0x000F: return 4;
            case 0x0010: return 8;
            case 0x0011: return 1;
            case 0x0015: return 2;
            case 0x0016: return 2;
            case 0x001A: return 6;
            case 0x001B: return 6;
            case 0x001C: return 0;
            case 0x001D: return 6;
            case 0x001E: return 0;
            case 0x001F: return 6;
            case 0x0020: return 8;
            case 0x0021: return 4;
            case 0x0022: return 6;
            case 0x0023: return 2;
            case 0x0028: case 0x0029: case 0x002A: case 0x002B:
                return ReadTextOpcodeSize(opcode, ref reader);
            case 0x002C: case 0x002D: case 0x002E: case 0x002F:
                if (reader.Remaining < 2) return null;
                return reader.ReadUInt16();
            case 0x0030: case 0x0031: case 0x0032: case 0x0033: case 0x0034:
            case 0x0035: case 0x0036: case 0x0037:
            case 0x0040: case 0x0041: case 0x0042: case 0x0043: case 0x0044:
            case 0x0045: case 0x0046: case 0x0047:
            case 0x0050: case 0x0051: case 0x0052: case 0x0053: case 0x0054:
            case 0x0055: case 0x0056: case 0x0057:
                return 8;
            case 0x0038: case 0x0039: case 0x003A: case 0x003B: case 0x003C:
            case 0x003D: case 0x003E: case 0x003F:
            case 0x0048: case 0x0049: case 0x004A: case 0x004B: case 0x004C:
            case 0x004D: case 0x004E: case 0x004F:
            case 0x0058: case 0x0059: case 0x005A: case 0x005B: case 0x005C:
            case 0x005D: case 0x005E: case 0x005F:
                return 0;
            case 0x0060: case 0x0061: case 0x0062: case 0x0063: case 0x0064:
            case 0x0065: case 0x0066: case 0x0067: return 12;
            case 0x0068: case 0x0069: case 0x006A: case 0x006B: case 0x006C:
            case 0x006D: case 0x006E: case 0x006F: return 4;
            case 0x0070: case 0x0071: case 0x0072: case 0x0073: case 0x0074:
            case 0x0075: case 0x0076: case 0x0077:
                if (reader.Remaining < 2) return null;
                return Math.Max(0, reader.ReadUInt16() - 2);
            case 0x0078: case 0x0079: case 0x007A: case 0x007B: case 0x007C:
            case 0x007D: case 0x007E: case 0x007F: return 0;
            case 0x0080: case 0x0081: case 0x0082: case 0x0083: case 0x0084:
            case 0x0085: case 0x0086: case 0x0087:
                if (reader.Remaining < 2) return null;
                return Math.Max(0, reader.ReadUInt16() - 2);
            case 0x008C: case 0x008D: case 0x008E: case 0x008F: return 0;
            case 0x00A0: return 2;
            case 0x00A1:
                if (reader.Remaining < 4) return null;
                reader.Skip(2);
                return reader.ReadUInt16();
            case 0x8200: case 0x8201:
                if (reader.Remaining < 4) return null;
                return (int)reader.ReadUInt32();
            default: return null;
        }
    }

    private static int ReadTextOpcodeSize(ushort opcode, ref BigEndianSpanReader r)
    {
        int prefix = opcode switch { 0x0028 => 4, 0x0029 => 1, 0x002A => 1, _ => 2 };
        if (r.Remaining < prefix + 1) return 0;
        r.Skip(prefix);
        byte len = r.ReadByte();
        return len;
    }
}
