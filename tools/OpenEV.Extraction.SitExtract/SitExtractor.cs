namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// Writes parsed SIT5 entries to disk in the SheepShaver extfs sidecar layout used
/// throughout this repo (see <c>OpenEV.Platform.ResourceFork.MacForkFile</c> and the existing
/// "EV Override 1.0.2 Ä" tree): the data fork at <c>&lt;name&gt;</c>, the resource fork
/// at <c>.rsrc\&lt;name&gt;</c> (only when non-empty), and 32 bytes of Finder info at
/// <c>.finf\&lt;name&gt;</c> — for files FInfo (type, creator, flags) + zeroed FXInfo;
/// for folders DInfo with the cosmetic window rect zeroed. Both decompressed forks are
/// verified against the CRC-16 stored in the archive.
/// </summary>
internal sealed class SitExtractor(byte[] archiveData, string outputDirectory)
{
    public int FilesWritten { get; private set; }
    public int DirectoriesWritten { get; private set; }
    public List<string> CrcMismatches { get; } = [];

    private static readonly DateTime MacEpoch = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Local);

    public void Extract(IEnumerable<Sit5Entry> entries)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (var entry in entries) ExtractEntry(entry, outputDirectory, "");
    }

    private void ExtractEntry(Sit5Entry entry, string directory, string relativePath)
    {
        string name = NameSanitizer.Sanitize(entry.Name);
        string path = Path.Combine(directory, name);
        string entryRelativePath = relativePath.Length == 0 ? entry.Name : relativePath + "/" + entry.Name;

        switch (entry)
        {
            case Sit5DirectoryEntry dir:
                Directory.CreateDirectory(path);
                WriteFinderInfo(directory, name, BuildDirectoryFinderInfo(dir));
                DirectoriesWritten++;
                Console.WriteLine($"  dir  {entryRelativePath}/");
                foreach (var child in dir.Children) ExtractEntry(child, path, entryRelativePath);
                Directory.SetLastWriteTime(path, MacDate(dir.ModificationDate)); // after children, or their writes bump it
                break;

            case Sit5FileEntry file:
                ExtractFile(file, directory, name, path, entryRelativePath);
                break;
        }
    }

    private void ExtractFile(Sit5FileEntry file, string directory, string name, string path, string entryRelativePath)
    {
        (byte[] resourceFork, string rsrcStatus) = DecodeAndVerifyFork(file, resource: true, entryRelativePath);
        (byte[] dataFork, string dataStatus) = DecodeAndVerifyFork(file, resource: false, entryRelativePath);

        File.WriteAllBytes(path, dataFork); // always, even when empty — the data fork is the file
        if (resourceFork.Length > 0)
        {
            string rsrcDir = Path.Combine(directory, ".rsrc");
            Directory.CreateDirectory(rsrcDir);
            File.WriteAllBytes(Path.Combine(rsrcDir, name), resourceFork);
        }
        WriteFinderInfo(directory, name, BuildFileFinderInfo(file));
        File.SetLastWriteTime(path, MacDate(file.ModificationDate));

        FilesWritten++;
        Console.WriteLine($"  file {entryRelativePath}  data {dataFork.Length} {dataStatus}, rsrc {resourceFork.Length} {rsrcStatus}");
    }

    /// <summary>
    /// Decode one fork and verify it against the CRC stored in the archive. A method-13
    /// fork that fails gets one retry via
    /// <see cref="StuffIt13Decompressor.DecompressDroppingLeadingPad"/> (see its docs for
    /// the encoder-pad quirk), accepted only if the CRC then passes.
    /// </summary>
    private (byte[] Fork, string Status) DecodeAndVerifyFork(Sit5FileEntry file, bool resource, string entryRelativePath)
    {
        string forkName = resource ? "resource fork" : "data fork";
        uint length = resource ? file.ResourceLength : file.DataLength;
        uint compressedLength = resource ? file.ResourceCompressedLength : file.DataCompressedLength;
        byte method = resource ? file.ResourceMethod : file.DataMethod;
        ushort expectedCrc = resource ? file.ResourceCrc : file.DataCrc;
        long start = resource ? file.DataStart : file.DataStart + file.ResourceCompressedLength;

        if ((resource && !file.HasResourceFork) || (length == 0 && compressedLength == 0))
            return ([], "ok");

        var compressed = archiveData.AsSpan((int)start, (int)compressedLength);
        byte[] fork = method switch
        {
            0 => compressedLength == length
                ? compressed.ToArray()
                : throw new InvalidDataException($"\"{entryRelativePath}\": stored {forkName} sizes disagree ({compressedLength} vs {length})."),
            13 => StuffIt13Decompressor.Decompress(compressed.ToArray(), (int)length),
            15 => throw new NotSupportedException($"\"{entryRelativePath}\": {forkName} uses method 15 (Arsenic), which is not implemented."),
            _ => throw new NotSupportedException($"\"{entryRelativePath}\": {forkName} uses unknown compression method {method}."),
        };

        ushort actual = Crc16.Compute(fork);
        if (actual == expectedCrc) return (fork, "ok");

        if (method == 13
            && StuffIt13Decompressor.DecompressDroppingLeadingPad(compressed.ToArray(), (int)length) is { } retried
            && Crc16.Compute(retried) == expectedCrc)
        {
            return (retried, "ok (leading encoder pad dropped)");
        }

        CrcMismatches.Add($"\"{entryRelativePath}\": {forkName} CRC mismatch (stored 0x{expectedCrc:X4}, computed 0x{actual:X4}).");
        return (fork, "CRC MISMATCH");
    }

    /// <summary>FInfo (type, creator, fdFlags, zeroed fdLocation/fdFldr) + zeroed FXInfo.</summary>
    private static byte[] BuildFileFinderInfo(Sit5FileEntry file)
    {
        var info = new byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(info, file.FileType);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(info.AsSpan(4), file.FileCreator);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(8), file.FinderFlags);
        return info;
    }

    /// <summary>DInfo (window rect zeroed — Finder cosmetics — then frFlags) + zeroed DXInfo.</summary>
    private static byte[] BuildDirectoryFinderInfo(Sit5DirectoryEntry dir)
    {
        var info = new byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(8), dir.FinderFlags);
        return info;
    }

    private static void WriteFinderInfo(string directory, string name, byte[] info)
    {
        string finfDir = Path.Combine(directory, ".finf");
        Directory.CreateDirectory(finfDir);
        File.WriteAllBytes(Path.Combine(finfDir, name), info);
    }

    private static DateTime MacDate(uint secondsSince1904) => MacEpoch.AddSeconds(secondsSince1904);
}
