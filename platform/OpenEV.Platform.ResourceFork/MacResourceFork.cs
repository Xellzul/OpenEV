using System.Text;

namespace OpenEV.Platform.ResourceFork;

/// <summary>
/// Reader/writer for the classic Mac resource-fork binary layout
/// (Inside Macintosh: More Macintosh Toolbox, ch. 1). The structure is:
/// <code>
///   [16-byte header: dataOff, mapOff, dataLen, mapLen]
///   [reserved up to dataOff]
///   [data section: per resource  u32 length + payload]
///   [resource map: 16-byte header echo, u32 nextMap, u16 fileRef, u16 attrs,
///                  u16 typeListOff, u16 nameListOff, type list, ref lists, name list]
/// </code>
/// Read preserves resource order and the per-resource attribute byte so a round-trip
/// produces a faithful fork. <see cref="ForkResource.TypeCode"/> gives the Mac Roman
/// display form of each resource's 4 OSType bytes.
/// </summary>
public static class MacResourceFork
{
    private const int HeaderSize = 16;
    // map = 16 (header echo) + nextMap(4) + fileRef(2) + attrs(2) + typeListOff(2) + nameListOff(2)
    private const int MapHeaderSize = 28;
    public const int DefaultDataStart = 0x100;

    /// <summary>Parse a fork, preserving resource order and the per-resource attribute byte.</summary>
    public static List<ForkResource> Read(byte[] fork)
    {
        var results = new List<ForkResource>();
        if (fork.Length < HeaderSize) return results;

        var r = new BigEndianSpanReader(fork);
        uint dataOff = r.ReadUInt32();
        uint mapOff = r.ReadUInt32();
        r.ReadUInt32(); // dataLen
        r.ReadUInt32(); // mapLen

        if (mapOff + MapHeaderSize > (uint)fork.Length) return results;

        r.Seek((int)mapOff + 16 + 4 + 2 + 2); // skip header echo + nextMap + fileRef + attrs
        ushort typeListOffset = r.ReadUInt16();
        ushort nameListOffset = r.ReadUInt16();

        int typeListAbs = (int)mapOff + typeListOffset;
        int nameListAbs = (int)mapOff + nameListOffset;

        r.Seek(typeListAbs);
        ushort numTypesMinus1 = r.ReadUInt16();
        int numTypes = numTypesMinus1 == 0xFFFF ? 0 : numTypesMinus1 + 1;

        var typeEntries = new List<(uint RawType, int Count, int RefListAbs)>(numTypes);
        for (int i = 0; i < numTypes; i++)
        {
            var raw4 = r.ReadBytes(4);
            uint rawType = ((uint)raw4[0] << 24) | ((uint)raw4[1] << 16) | ((uint)raw4[2] << 8) | raw4[3];
            ushort cntMinus1 = r.ReadUInt16();
            ushort refListRel = r.ReadUInt16();
            typeEntries.Add((rawType, cntMinus1 + 1, typeListAbs + refListRel));
        }

        foreach (var (rawType, count, refListAbs) in typeEntries)
        {
            for (int i = 0; i < count; i++)
            {
                int refPos = refListAbs + i * 12;
                if (refPos + 12 > fork.Length) continue;
                r.Seek(refPos);
                short id = r.ReadInt16();
                short nameOff = r.ReadInt16();
                uint attrAndOffset = r.ReadUInt32();
                byte attrs = (byte)(attrAndOffset >> 24);
                int dataOffset = (int)(attrAndOffset & 0x00FFFFFFu);

                int dataPos = (int)dataOff + dataOffset;
                if (dataPos + 4 > fork.Length) continue;
                r.Seek(dataPos);
                uint payloadLen = r.ReadUInt32();
                if (dataPos + 4 + payloadLen > (uint)fork.Length) continue;
                byte[] payload = r.ReadBytes((int)payloadLen).ToArray();

                string? name = null;
                if (nameOff >= 0)
                {
                    int namePos = nameListAbs + nameOff;
                    if (namePos < fork.Length)
                    {
                        r.Seek(namePos);
                        byte nameLen = r.ReadByte();
                        if (namePos + 1 + nameLen <= fork.Length)
                            name = Encoding.Latin1.GetString(r.ReadBytes(nameLen));
                    }
                }
                results.Add(new ForkResource(rawType, id, name, payload, attrs));
            }
        }
        return results;
    }

    /// <summary>Serialize resources into a valid resource fork the reader round-trips losslessly.</summary>
    public static byte[] Write(IReadOnlyList<ForkResource> resources, int dataStart = DefaultDataStart)
    {
        // Group by type, preserving first-seen type order and within-type order.
        var typeOrder = new List<uint>();
        var byType = new Dictionary<uint, List<ForkResource>>();
        foreach (var res in resources)
        {
            if (!byType.TryGetValue(res.RawType, out var list))
            {
                list = [];
                byType[res.RawType] = list;
                typeOrder.Add(res.RawType);
            }
            list.Add(res);
        }

        // Data section + per-resource data offsets (relative to dataStart).
        var dataW = new BigEndianWriter(1024);
        var dataOffsets = new Dictionary<ForkResource, int>(ReferenceComparer.Instance);
        foreach (uint type in typeOrder)
            foreach (var res in byType[type])
            {
                dataOffsets[res] = dataW.Length;
                dataW.WriteUInt32((uint)res.Data.Length);
                dataW.WriteBytes(res.Data);
            }

        // Name list + per-resource name offsets (relative to name-list start; -1 = no name).
        var nameW = new BigEndianWriter(256);
        var nameOffsets = new Dictionary<ForkResource, int>(ReferenceComparer.Instance);
        foreach (uint type in typeOrder)
            foreach (var res in byType[type])
            {
                if (res.Name is null) { nameOffsets[res] = -1; continue; }
                nameOffsets[res] = nameW.Length;
                byte[] nameBytes = Encoding.Latin1.GetBytes(res.Name);
                if (nameBytes.Length > 255) nameBytes = nameBytes[..255];
                nameW.WriteByte((byte)nameBytes.Length);
                nameW.WriteBytes(nameBytes);
            }

        // Type list + ref lists.
        int numTypes = typeOrder.Count;
        int refBase = 2 + numTypes * 8; // offset (from type-list start) of the first ref list
        var tlW = new BigEndianWriter(256);
        tlW.WriteUInt16((ushort)(numTypes == 0 ? 0xFFFF : numTypes - 1));
        int cum = 0;
        foreach (uint type in typeOrder)
        {
            int count = byType[type].Count;
            tlW.WriteByte((byte)(type >> 24));
            tlW.WriteByte((byte)(type >> 16));
            tlW.WriteByte((byte)(type >> 8));
            tlW.WriteByte((byte)type);
            tlW.WriteUInt16((ushort)(count - 1));
            tlW.WriteUInt16((ushort)(refBase + cum * 12));
            cum += count;
        }
        foreach (uint type in typeOrder)
            foreach (var res in byType[type])
            {
                tlW.WriteInt16(res.Id);
                tlW.WriteInt16((short)nameOffsets[res]);
                tlW.WriteByte(res.Attributes);
                tlW.WriteUInt24((uint)dataOffsets[res]);
                tlW.WriteUInt32(0); // in-memory handle placeholder
            }

        int typeListLen = tlW.Length;
        int nameListLen = nameW.Length;
        int dataLen = dataW.Length;
        int mapLen = MapHeaderSize + typeListLen + nameListLen;
        int mapOff = dataStart + dataLen;

        var w = new BigEndianWriter(mapOff + mapLen);
        // Header.
        w.WriteUInt32((uint)dataStart);
        w.WriteUInt32((uint)mapOff);
        w.WriteUInt32((uint)dataLen);
        w.WriteUInt32((uint)mapLen);
        w.Zero(dataStart - HeaderSize); // reserved-for-system region
        w.WriteBytes(dataW.WrittenSpan);
        // Resource map — 16-byte header echo (a copy of the fork header, per Inside Macintosh).
        w.WriteUInt32((uint)dataStart);
        w.WriteUInt32((uint)mapOff);
        w.WriteUInt32((uint)dataLen);
        w.WriteUInt32((uint)mapLen);
        w.WriteUInt32(0);                            // next resource map
        w.WriteUInt16(0);                            // file ref num
        w.WriteUInt16(0);                            // fork attributes
        w.WriteUInt16(MapHeaderSize);                // type list offset (from map start)
        w.WriteUInt16((ushort)(MapHeaderSize + typeListLen)); // name list offset
        w.WriteBytes(tlW.WrittenSpan);
        w.WriteBytes(nameW.WrittenSpan);

        return w.ToArray();
    }

    // Identity comparer so two ForkResource records with equal field values still key separately.
    private sealed class ReferenceComparer : IEqualityComparer<ForkResource>
    {
        public static readonly ReferenceComparer Instance = new();
        public bool Equals(ForkResource? x, ForkResource? y) => ReferenceEquals(x, y);
        public int GetHashCode(ForkResource obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
