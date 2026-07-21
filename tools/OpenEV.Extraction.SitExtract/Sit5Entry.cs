namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// One entry parsed from a StuffIt 5 archive. Names keep both the decoded string and
/// the raw archive bytes (SIT5 names are MacRoman classically, but archives written by
/// later tools store UTF-8 — the EV Override 1.0.2 archive does).
/// </summary>
internal abstract class Sit5Entry(string name, byte[] rawName, long headerOffset, uint creationDate, uint modificationDate, ushort finderFlags)
{
    public string Name { get; } = name;
    public byte[] RawName { get; } = rawName;
    /// <summary>Archive offset of this entry's header (the 0xA5A5A5A5 magic).</summary>
    public long HeaderOffset { get; } = headerOffset;
    /// <summary>Seconds since the classic Mac epoch (1904-01-01, nominally local time).</summary>
    public uint CreationDate { get; } = creationDate;
    public uint ModificationDate { get; } = modificationDate;
    public ushort FinderFlags { get; } = finderFlags;
}

/// <summary>A folder entry; its children physically follow it in the archive.</summary>
internal sealed class Sit5DirectoryEntry(string name, byte[] rawName, long headerOffset, uint creationDate, uint modificationDate, ushort finderFlags)
    : Sit5Entry(name, rawName, headerOffset, creationDate, modificationDate, finderFlags)
{
    public List<Sit5Entry> Children { get; } = [];
}

/// <summary>
/// A file entry. The compressed resource fork sits first at <see cref="DataStart"/>,
/// immediately followed by the compressed data fork.
/// </summary>
internal sealed class Sit5FileEntry(string name, byte[] rawName, long headerOffset, uint creationDate, uint modificationDate, ushort finderFlags)
    : Sit5Entry(name, rawName, headerOffset, creationDate, modificationDate, finderFlags)
{
    public required uint FileType { get; init; }
    public required uint FileCreator { get; init; }

    public required uint DataLength { get; init; }
    public required uint DataCompressedLength { get; init; }
    public required ushort DataCrc { get; init; }
    public required byte DataMethod { get; init; }

    public required bool HasResourceFork { get; init; }
    public required uint ResourceLength { get; init; }
    public required uint ResourceCompressedLength { get; init; }
    public required ushort ResourceCrc { get; init; }
    public required byte ResourceMethod { get; init; }

    /// <summary>Archive offset where the compressed resource fork begins (data fork follows it).</summary>
    public required long DataStart { get; init; }
}
