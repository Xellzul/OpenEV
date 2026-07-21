using System.Text;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// StuffIt 5 (.sit) container parser, a direct port of the entry walk in XADMaster's
/// <c>XADStuffIt5Parser.m</c> (© Dag Ågren / MacPaw, LGPL 2.1+,
/// https://github.com/MacPaw/XADMaster). Entries are read sequentially: a directory's
/// children physically follow it, a file's next entry follows its two compressed forks,
/// and directory-flagged entries whose data-length field is 0xFFFFFFFF are undocumented
/// 48-byte sentinels that appear after directory contents and are skipped (they extend
/// the entry count by one, exactly like XAD does).
/// </summary>
internal sealed class Sit5Archive
{
    private const uint EntryMagic = 0xA5A5A5A5;

    private const byte FlagDirectory = 0x40;
    private const byte FlagEncrypted = 0x20;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public required List<Sit5Entry> Roots { get; init; }

    /// <summary>Archive offset right after the last parsed entry's data — should equal the file size.</summary>
    public required long EndOffset { get; init; }

    public static Sit5Archive Parse(byte[] data)
    {
        var r = new BigEndianSpanReader(data);

        // 80-byte signature ("StuffIt (c)1997-… Aladdin Systems…" + CRLF) + 0x1A 0x00.
        // Like XAD, only the stable prefix is matched; the year range varies.
        if (data.Length < 100 || !data.AsSpan(0, 16).SequenceEqual("StuffIt (c)1997-"u8))
            throw new InvalidDataException("Not a StuffIt 5 archive (signature mismatch).");

        r.Seek(82);
        int archiveVersion = r.ReadByte();
        int archiveFlags = r.ReadByte();
        if (archiveVersion != 5)
            throw new InvalidDataException($"Unsupported StuffIt archive version {archiveVersion} (expected 5).");
        if ((archiveFlags & 0x80) != 0)
            throw new NotSupportedException("Encrypted StuffIt archives are not supported.");

        uint totalSize = r.ReadUInt32();
        if (totalSize != data.Length)
            Console.Error.WriteLine($"warning: archive header claims {totalSize} bytes but the file has {data.Length}.");
        r.Skip(4); // unknown
        int numEntries = r.ReadUInt16(); // number of top-level entries
        uint firstOffset = r.ReadUInt32();

        r.Seek((int)firstOffset);

        var directoriesByOffset = new Dictionary<uint, Sit5DirectoryEntry>();
        var roots = new List<Sit5Entry>();

        for (int i = 0; i < numEntries; i++)
        {
            uint entryOffset = (uint)r.Position;

            if (r.ReadUInt32() != EntryMagic)
                throw new InvalidDataException($"Expected entry magic at archive offset 0x{entryOffset:X}.");

            int entryVersion = r.ReadByte();
            r.Skip(1);
            int headerSize = r.ReadUInt16();
            long headerEnd = entryOffset + headerSize;
            r.Skip(1);
            byte flags = (byte)r.ReadByte();
            uint creationDate = r.ReadUInt32();
            uint modificationDate = r.ReadUInt32();
            r.Skip(8); // offsets of previous/next entry (redundant with the sequential walk)
            uint parentOffset = r.ReadUInt32();
            int nameLength = r.ReadUInt16();
            ushort headerCrc = r.ReadUInt16();
            uint dataLength = r.ReadUInt32();
            uint dataCompressedLength = r.ReadUInt32();
            ushort dataCrc = r.ReadUInt16();
            r.Skip(2); // unknown

            VerifyHeaderCrc(data, entryOffset, headerSize, headerCrc);

            bool isDirectory = (flags & FlagDirectory) != 0;
            byte dataMethod = 0;
            int childCount = 0;
            if (isDirectory)
            {
                childCount = r.ReadUInt16();

                // Undocumented sentinel entries (see class docs): skip, reader is already
                // at the next entry because their header is exactly the fixed 48 bytes.
                if (dataLength == 0xFFFFFFFF) { numEntries++; continue; }
            }
            else
            {
                dataMethod = (byte)r.ReadByte();
                int passwordLength = r.ReadByte();
                if ((flags & FlagEncrypted) != 0 || passwordLength != 0)
                    throw new NotSupportedException($"Entry at archive offset 0x{entryOffset:X} is encrypted; not supported.");
            }

            byte[] rawName = r.ReadBytes(nameLength).ToArray();

            if (r.Position < headerEnd)
            {
                int commentSize = r.ReadUInt16();
                r.Skip(2);
                r.Skip(commentSize); // entry comment, not preserved
            }

            // Metadata block after the header proper.
            int forkFlags = r.ReadUInt16(); // bit 0: resource fork present
            r.Skip(2);
            uint fileType = r.ReadUInt32();
            uint fileCreator = r.ReadUInt32();
            ushort finderFlags = r.ReadUInt16();
            r.Skip(entryVersion == 1 ? 22 : 18);

            bool hasResource = (forkFlags & 0x01) != 0;
            uint resourceLength = 0, resourceCompressedLength = 0;
            ushort resourceCrc = 0;
            byte resourceMethod = 0;
            if (hasResource)
            {
                resourceLength = r.ReadUInt32();
                resourceCompressedLength = r.ReadUInt32();
                resourceCrc = r.ReadUInt16();
                r.Skip(2);
                resourceMethod = (byte)r.ReadByte();
                int passwordLength = r.ReadByte();
                if (passwordLength != 0)
                    throw new NotSupportedException($"Entry at archive offset 0x{entryOffset:X} has an encrypted resource fork; not supported.");
            }

            long dataStart = r.Position;

            string name = DecodeName(rawName);
            directoriesByOffset.TryGetValue(parentOffset, out var parent);
            var siblings = parent?.Children ?? roots;

            if (isDirectory)
            {
                var dir = new Sit5DirectoryEntry(name, rawName, entryOffset, creationDate, modificationDate, finderFlags);
                directoriesByOffset[entryOffset] = dir;
                siblings.Add(dir);
                numEntries += childCount;
                // Children follow immediately; the reader is already positioned on them.
            }
            else
            {
                siblings.Add(new Sit5FileEntry(name, rawName, entryOffset, creationDate, modificationDate, finderFlags)
                {
                    FileType = fileType,
                    FileCreator = fileCreator,
                    DataLength = dataLength,
                    DataCompressedLength = dataCompressedLength,
                    DataCrc = dataCrc,
                    DataMethod = dataMethod,
                    HasResourceFork = hasResource,
                    ResourceLength = resourceLength,
                    ResourceCompressedLength = resourceCompressedLength,
                    ResourceCrc = resourceCrc,
                    ResourceMethod = resourceMethod,
                    DataStart = dataStart,
                });
                r.Seek((int)(dataStart + resourceCompressedLength + dataCompressedLength));
            }
        }

        // Directories that nothing follows leave their end-of-directory sentinels after
        // the last counted entry, where the count-based walk never reaches them (the
        // archive here ends with two: one for the last folder, one for the root).
        // Consume them so EndOffset == file size holds for a fully understood archive.
        while (r.Position + 48 <= data.Length && IsSentinelAt(data, r.Position))
        {
            uint entryOffset = (uint)r.Position;
            int headerSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r.Position + 6));
            ushort headerCrc = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r.Position + 32));
            VerifyHeaderCrc(data, entryOffset, headerSize, headerCrc);
            r.Seek((int)(entryOffset + headerSize));
        }

        return new Sit5Archive { Roots = roots, EndOffset = r.Position };
    }

    private static bool IsSentinelAt(byte[] data, int position)
    {
        var span = data.AsSpan(position);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(span) == EntryMagic
            && (span[9] & FlagDirectory) != 0
            && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(span[34..]) == 0xFFFFFFFF;
    }

    private static void VerifyHeaderCrc(byte[] data, uint entryOffset, int headerSize, ushort expected)
    {
        if (headerSize < 48 || entryOffset + headerSize > data.Length)
            throw new InvalidDataException($"Entry at archive offset 0x{entryOffset:X} has implausible header size {headerSize}.");

        // CRC-16 of the whole header with the CRC field (bytes 32-33) cleared.
        var header = data.AsSpan((int)entryOffset, headerSize);
        ushort crc = Crc16.Update(0, header[..32]);
        crc = Crc16.Update(crc, [0, 0]);
        crc = Crc16.Update(crc, header[34..]);
        if (crc != expected)
            throw new InvalidDataException($"Entry at archive offset 0x{entryOffset:X} fails its header CRC (stored 0x{expected:X4}, computed 0x{crc:X4}).");
    }

    /// <summary>
    /// SIT5 names are MacRoman classically, but archives written by later tools carry
    /// UTF-8 (this repo's EV Override archive stores 'ƒ' as C6 92). Strict UTF-8 decode
    /// first, MacRoman fallback — pure-ASCII names are identical either way.
    /// </summary>
    private static string DecodeName(byte[] rawName)
    {
        try { return StrictUtf8.GetString(rawName); }
        catch (DecoderFallbackException) { return MacRoman.GetString(rawName); }
    }
}
