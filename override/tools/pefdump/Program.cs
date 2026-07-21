using System;
using System.IO;
using System.Collections.Generic;

// Decompress a PEF pattern-initialized data section and locate the game-speed
// scale constants by anchoring on the int->double magic 0x4330000080000000,
// which the compiler places at both toc-0x6950 and toc-0x6598 (0x3b8 apart).

static uint BE32(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

string path = args[0];
byte[] f = File.ReadAllBytes(path);
int sc = (b: f, o: 0x20).b[0x20] << 8 | f[0x21];
int hdr = 0x28;
(uint defAddr, uint total, uint unp, uint pk, uint cOff, int kind) Sec(int i)
{
    int o = hdr + i * 0x1c;
    return (BE32(f, o + 4), BE32(f, o + 8), BE32(f, o + 0xc), BE32(f, o + 0x10), BE32(f, o + 0x14), f[o + 0x18]);
}
// Find the pattern-initialized data section (kind == 2).
int dataIdx = -1;
for (int i = 0; i < sc; i++) if (Sec(i).kind == 2) { dataIdx = i; break; }
var s = Sec(dataIdx);
Console.WriteLine($"data section {dataIdx}: kind=2 total=0x{s.total:x} unpacked=0x{s.unp:x} packed=0x{s.pk:x} containerOff=0x{s.cOff:x}");

// --- pidata decompression ---
byte[] packed = new byte[s.pk];
Array.Copy(f, (int)s.cOff, packed, 0, (int)s.pk);
var outp = new List<byte>((int)s.unp);
int ip = 0;
long ReadCount()
{
    long r = 0; byte bb;
    do { bb = packed[ip++]; r = (r << 7) | (uint)(bb & 0x7f); } while ((bb & 0x80) != 0);
    return r;
}
while (ip < packed.Length)
{
    byte op = packed[ip++];
    int opcode = op >> 5;
    long count = op & 0x1f;
    if (count == 0) count = ReadCount();
    switch (opcode)
    {
        case 0: // zero
            for (long i = 0; i < count; i++) outp.Add(0);
            break;
        case 1: // blockCopy
            for (long i = 0; i < count; i++) outp.Add(packed[ip++]);
            break;
        case 2: // repeatedBlock: block of 'count' bytes, repeated (repeatCount+1) times
        {
            long rep = ReadCount();
            int start = ip; ip += (int)count;
            for (long r = 0; r <= rep; r++)
                for (long i = 0; i < count; i++) outp.Add(packed[start + (int)i]);
            break;
        }
        case 3: // interleaveRepeatBlockWithBlockCopy: common(count), then rep*(custom+common)
        {
            long customSize = ReadCount();
            long rep = ReadCount();
            int common = ip; ip += (int)count;
            for (long i = 0; i < count; i++) outp.Add(packed[common + (int)i]);
            for (long r = 0; r < rep; r++)
            {
                for (long i = 0; i < customSize; i++) outp.Add(packed[ip++]);
                for (long i = 0; i < count; i++) outp.Add(packed[common + (int)i]);
            }
            break;
        }
        case 4: // interleaveRepeatBlockWithZero: zero(count), then rep*(custom+zero)
        {
            long customSize = ReadCount();
            long rep = ReadCount();
            for (long i = 0; i < count; i++) outp.Add(0);
            for (long r = 0; r < rep; r++)
            {
                for (long i = 0; i < customSize; i++) outp.Add(packed[ip++]);
                for (long i = 0; i < count; i++) outp.Add(0);
            }
            break;
        }
    }
}
byte[] d = outp.ToArray();
Console.WriteLine($"decompressed {d.Length} bytes (expected 0x{s.unp:x} = {s.unp}) -> {(d.Length == s.unp ? "MATCH" : "MISMATCH")}");

// Find magic 0x4330000080000000.
var magics = new List<int>();
for (int i = 0; i + 8 <= d.Length; i++)
    if (d[i] == 0x43 && d[i + 1] == 0x30 && d[i + 2] == 0 && d[i + 3] == 0 &&
        d[i + 4] == 0x80 && d[i + 5] == 0 && d[i + 6] == 0 && d[i + 7] == 0)
        magics.Add(i);
Console.WriteLine($"magic occurrences at section offsets: {string.Join(", ", magics.ConvertAll(x => "0x" + x.ToString("x")))}");

double RD(int off) // big-endian double at section offset
{
    byte[] t = new byte[8];
    Array.Copy(d, off, t, 0, 8);
    Array.Reverse(t);
    return BitConverter.ToDouble(t, 0);
}
// Identify the toc-0x6950 / toc-0x6598 pair: 0x3b8 apart (0x6950-0x6598).
foreach (int m in magics)
    foreach (int n in magics)
        if (n - m == 0x3b8)
        {
            // m == offset of toc-0x6950 ; n == offset of toc-0x6598
            Console.WriteLine($"\nPAIR found: toc-0x6950 @ off 0x{m:x}, toc-0x6598 @ off 0x{n:x}");
            Console.WriteLine($"  B    = *(toc-0x6958) @ off 0x{m - 8:x}  = {RD(m - 8):R}");
            Console.WriteLine($"  magic= *(toc-0x6950) @ off 0x{m:x}      = {RD(m):R}");
            // The ship/weapon default-stat slots live between the magic and A.
            // Each prints as int / float / double so the field width is unambiguous.
            void Slot(string label, int slotDelta) // slotDelta = (0x6950 - target) i.e. distance past the magic
            {
                int off = m + slotDelta;
                int iv = (int)BE32(d, off);
                byte[] t4 = { d[off + 3], d[off + 2], d[off + 1], d[off] };
                float fv = BitConverter.ToSingle(t4, 0);
                Console.WriteLine($"  {label} @ off 0x{off:x} = int:{iv}  float:{fv:R}  double:{RD(off):R}");
            }
            Slot("toc-0x6944", 0x6950 - 0x6944);
            Slot("toc-0x6940", 0x6950 - 0x6940);
            Slot("toc-0x693c", 0x6950 - 0x693c);
            Slot("toc-0x6938", 0x6950 - 0x6938);
            Console.WriteLine($"  A    = *(toc-0x6930) @ off 0x{m + 0x20:x} = {RD(m + 0x20):R}");
            Console.WriteLine($"  C    = *(toc-0x6928) @ off 0x{m + 0x28:x} = {RD(m + 0x28):R}");
            Console.WriteLine($"  C1   = *(toc-0x65a8) @ off 0x{n - 0x10:x} = {RD(n - 0x10):R}");
            Console.WriteLine($"  C2   = *(toc-0x65a0) @ off 0x{n - 8:x}  = {RD(n - 8):R}");
            Console.WriteLine($"  m2   = *(toc-0x6598) @ off 0x{n:x}      = {RD(n):R}");
        }
