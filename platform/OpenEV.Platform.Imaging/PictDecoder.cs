using System;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

public static class PictDecoder
{
    public static Rgba8Image? Decode(byte[] data, string? tag = null)
    {
        if (data.Length < 40) { Log(tag, $"too small for PICT v2 header ({data.Length} bytes)"); return null; }

        var reader = new BigEndianSpanReader(data);
        reader.Skip(2);
        short top = reader.ReadInt16(), left = reader.ReadInt16();
        short bottom = reader.ReadInt16(), right = reader.ReadInt16();
        int width = right - left;
        int height = bottom - top;
        if (width <= 0 || height <= 0 || width > 4000 || height > 4000)
        { Log(tag, $"invalid bounds {width}x{height}"); return null; }

        uint version = reader.ReadUInt32();
        if (version != 0x001102FF)
        {
            if ((version >> 16) == 0x1101)
            {
                reader.Seek(reader.Position - 2);
                return ClassicPictV1Decoder.Decode(ref reader, width, height, tag);
            }
            Log(tag, $"not PICT v2 (magic 0x{version:X8})");
            return null;
        }
        reader.Skip(2); reader.Skip(24);
        return TryParseOpcodes(ref reader, width, height, tag);
    }

    private static bool IsBitmap(ushort op) =>
        op == 0x0090 || op == 0x0091 || op == 0x0098 || op == 0x0099 || op == 0x009A || op == 0x009B;

    private static Rgba8Image? TryParseOpcodes(ref BigEndianSpanReader reader, int width, int height, string? tag)
    {
        while (reader.Remaining > 0)
        {
            if (reader.Position % 2 != 0) reader.Skip(1);
            if (reader.Remaining < 2) break;
            int opPos = reader.Position;
            ushort opcode = reader.ReadUInt16();
            if (opcode == 0x00FF) break;
            if (IsBitmap(opcode))
            {
                DecodeDiagnostics.Log(tag, $"bitmap op 0x{opcode:X4} at 0x{opPos:X4} {width}x{height}");
                return QuickDrawBitmapDecoder.Decode(ref reader, width, height, opcode, tag);
            }
            int? size = PictOpcodeTable.Skip(opcode, ref reader);
            if (size is null) { Log(tag, $"unknown opcode 0x{opcode:X4} at 0x{opPos:X4}"); return null; }
            if (size.Value > reader.Remaining) { Log(tag, $"op 0x{opcode:X4} wants {size.Value} bytes, only {reader.Remaining}"); return null; }
            reader.Skip(size.Value);
        }
        Log(tag, "no bitmap opcode found before end of PICT");
        return null;
    }

    private static void Log(string? tag, string m) =>
        Console.WriteLine(tag is null ? $"  [WARN] Pict: {m}" : $"  [WARN] {tag}: {m}");
}
