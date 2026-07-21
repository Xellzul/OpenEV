using System;

namespace OpenEV.Platform.Imaging;

public static class PackBitsDecompressor
{
    public static void Unpack(ReadOnlySpan<byte> packed, Span<byte> output)
    {
        int inPos = 0, outPos = 0, n = output.Length;
        while (inPos < packed.Length && outPos < n)
        {
            sbyte h = (sbyte)packed[inPos++];
            if (h >= 0)
            {
                int count = h + 1;
                for (int i = 0; i < count && outPos < n && inPos < packed.Length; i++)
                    output[outPos++] = packed[inPos++];
            }
            else if (h != -128)
            {
                if (inPos >= packed.Length) break;
                int count = -h + 1;
                byte v = packed[inPos++];
                for (int i = 0; i < count && outPos < n; i++) output[outPos++] = v;
            }
        }
    }

    public static void UnpackWords(ReadOnlySpan<byte> packed, Span<byte> output)
    {
        int inPos = 0, outPos = 0, n = output.Length;
        while (inPos < packed.Length && outPos < n)
        {
            sbyte h = (sbyte)packed[inPos++];
            if (h >= 0)
            {
                int count = (h + 1) * 2;
                for (int i = 0; i < count && outPos < n && inPos < packed.Length; i++)
                    output[outPos++] = packed[inPos++];
            }
            else if (h != -128)
            {
                if (inPos + 1 >= packed.Length) break;
                int count = -h + 1;
                byte b1 = packed[inPos++];
                byte b2 = packed[inPos++];
                for (int i = 0; i < count && outPos < n; i++)
                {
                    if (outPos < n) output[outPos++] = b1;
                    if (outPos < n) output[outPos++] = b2;
                }
            }
        }
    }
}
