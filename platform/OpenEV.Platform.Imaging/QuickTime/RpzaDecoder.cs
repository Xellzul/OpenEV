namespace OpenEV.Platform.Imaging.QuickTime;

// Apple Video ("road pizza", QuickTime fourcc 'rpza') — 4×4 blocks of RGB555 with
// skip / single-colour / four-colour-interpolated / sixteen-colour opcodes. Inter
// frames reuse the previous frame's pixels for skipped blocks, so the decoder keeps
// one persistent frame buffer. Layout per the long-established public description
// of the bitstream (multimedia.cx wiki / ffmpeg rpza.c).
public sealed class RpzaDecoder
{
    private readonly Rgba8Image _frame;
    private readonly int _blocksW;

    public RpzaDecoder(int width, int height)
    {
        _frame = new Rgba8Image(width, height);
        _blocksW = (width + 3) / 4;
    }

    public Rgba8Image DecodeFrame(ReadOnlySpan<byte> d)
    {
        // Chunk header: 0xE1 marker + 24-bit chunk length.
        if (d.Length < 4 || d[0] != 0xE1) return _frame;
        int i = 4;
        int totalBlocks = _blocksW * ((_frame.Height + 3) / 4);
        int block = 0;
        Span<ushort> color4 = stackalloc ushort[4];

        while (block < totalBlocks && i < d.Length)
        {
            byte opcode = d[i++];
            int n = (opcode & 0x1F) + 1;
            ushort colorA = 0;

            if ((opcode & 0x80) == 0)
            {
                // High bit clear: opcode byte is the top byte of colorA.
                if (i >= d.Length) break;
                colorA = (ushort)((opcode << 8) | d[i++]);
                if (i < d.Length && (d[i] & 0x80) != 0) { opcode = 0x20; n = 1; }  // one 4-colour block
                else opcode = 0x00;                                                // one 16-colour block
            }

            switch (opcode & 0xE0)
            {
                case 0x80:   // skip blocks (keep previous frame content)
                    block += n;
                    break;

                case 0xA0:   // n blocks of one colour
                {
                    if (i + 2 > d.Length) { block = totalBlocks; break; }
                    ushort c = (ushort)((d[i] << 8) | d[i + 1]); i += 2;
                    for (; n > 0 && block < totalBlocks; n--, block++) FillBlock(block, c);
                    break;
                }

                case 0xC0:   // n blocks of four interpolated colours
                case 0x20:   // one block, colorA already read
                {
                    if ((opcode & 0xE0) == 0xC0)
                    {
                        if (i + 2 > d.Length) { block = totalBlocks; break; }
                        colorA = (ushort)((d[i] << 8) | d[i + 1]); i += 2;
                    }
                    if (i + 2 > d.Length) { block = totalBlocks; break; }
                    ushort colorB = (ushort)((d[i] << 8) | d[i + 1]); i += 2;

                    // color4 = {B, 11/21 mix, 21/11 mix, A} per channel (ffmpeg's table).
                    color4[0] = colorB; color4[3] = colorA;
                    int c1 = 0, c2 = 0;
                    for (int shift = 10; shift >= 0; shift -= 5)
                    {
                        int ta = (colorA >> shift) & 0x1F, tb = (colorB >> shift) & 0x1F;
                        c1 |= ((11 * ta + 21 * tb) >> 5) << shift;
                        c2 |= ((21 * ta + 11 * tb) >> 5) << shift;
                    }
                    color4[1] = (ushort)c1; color4[2] = (ushort)c2;

                    for (; n > 0 && block < totalBlocks; n--, block++)
                    {
                        if (i + 4 > d.Length) { block = totalBlocks; break; }
                        for (int row = 0; row < 4; row++)
                        {
                            byte idx = d[i++];
                            for (int col = 0; col < 4; col++)
                                SetPixel(block, col, row, color4[(idx >> (6 - col * 2)) & 3]);
                        }
                    }
                    break;
                }

                default:     // 0x00: one block of 16 literal colours (colorA = pixel 0)
                {
                    for (int p = 0; p < 16 && block < totalBlocks; p++)
                    {
                        ushort c = colorA;
                        if (p > 0)
                        {
                            if (i + 2 > d.Length) { block = totalBlocks; break; }
                            c = (ushort)((d[i] << 8) | d[i + 1]); i += 2;
                        }
                        SetPixel(block, p & 3, p >> 2, c);
                    }
                    block++;
                    break;
                }
            }
        }
        return _frame;
    }

    private void FillBlock(int block, ushort c)
    {
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
                SetPixel(block, col, row, c);
    }

    private void SetPixel(int block, int col, int row, ushort rgb555)
    {
        int x = (block % _blocksW) * 4 + col;
        int y = (block / _blocksW) * 4 + row;
        if (x >= _frame.Width || y >= _frame.Height) return;
        int r = (rgb555 >> 10) & 0x1F, g = (rgb555 >> 5) & 0x1F, b = rgb555 & 0x1F;
        _frame.SetPixel(x, y,
            (byte)((r << 3) | (r >> 2)), (byte)((g << 3) | (g >> 2)), (byte)((b << 3) | (b >> 2)), 255);
    }
}
