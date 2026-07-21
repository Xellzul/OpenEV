using OpenEV.Platform.ResourceFork;

namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// StuffIt 5 (.sit) extractor. Extracts an archive into the SheepShaver sidecar layout
/// (data fork + <c>.rsrc\</c> + <c>.finf\</c>) used by the rest of this repo, verifying
/// every fork against the CRC-16 stored in the archive.
/// </summary>
internal static class Program
{
    private const string Usage =
        """
        usage: OpenEV.Extraction.SitExtract <archive.sit> [-o <outdir>] [--list]

          -o <outdir>  output directory (default: archive filename stem, next to the archive)
          --list       print the archive tree without extracting

        exit codes: 0 ok, 1 usage, 2 fatal (bad archive / unsupported), 3 extracted with CRC mismatches
        """;

    public static int Main(string[] args)
    {
        string? archivePath = null;
        string? outputDirectory = null;
        bool listOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o":
                    if (++i >= args.Length) return UsageError("-o requires a directory argument.");
                    outputDirectory = args[i];
                    break;
                case "--list":
                    listOnly = true;
                    break;
                case var flag when flag.StartsWith('-'):
                    return UsageError($"Unknown option \"{flag}\".");
                default:
                    if (archivePath is not null) return UsageError("More than one archive path given.");
                    archivePath = args[i];
                    break;
            }
        }
        if (archivePath is null) return UsageError(null);

        try
        {
            byte[] data = File.ReadAllBytes(archivePath);
            var archive = Sit5Archive.Parse(data);

            if (archive.EndOffset != data.Length)
                Console.Error.WriteLine($"warning: entry walk ended at offset 0x{archive.EndOffset:X} but the archive is 0x{data.Length:X} bytes.");

            if (listOnly)
            {
                foreach (var root in archive.Roots) PrintTree(root, 0);
                Console.WriteLine($"{CountFiles(archive.Roots)} files, {CountDirectories(archive.Roots)} directories.");
                return 0;
            }

            outputDirectory ??= Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(archivePath))!,
                Path.GetFileNameWithoutExtension(archivePath));

            Console.WriteLine($"Extracting to \"{outputDirectory}\":");
            var extractor = new SitExtractor(data, outputDirectory);
            extractor.Extract(archive.Roots);
            Console.WriteLine($"{extractor.FilesWritten} files, {extractor.DirectoriesWritten} directories written.");

            if (extractor.CrcMismatches.Count > 0)
            {
                Console.Error.WriteLine($"{extractor.CrcMismatches.Count} fork(s) failed CRC verification:");
                foreach (string mismatch in extractor.CrcMismatches) Console.Error.WriteLine($"  {mismatch}");
                return 3;
            }
            return 0;
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or EndOfStreamException or IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 2;
        }
    }

    private static int UsageError(string? message)
    {
        if (message is not null) Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return 1;
    }

    private static void PrintTree(Sit5Entry entry, int depth)
    {
        string indent = new(' ', depth * 2);
        switch (entry)
        {
            case Sit5DirectoryEntry dir:
                Console.WriteLine($"{indent}{dir.Name}/");
                foreach (var child in dir.Children) PrintTree(child, depth + 1);
                break;
            case Sit5FileEntry file:
                string forks = $"data {file.DataLength}→{file.DataCompressedLength} m{file.DataMethod}";
                if (file.HasResourceFork)
                    forks += $", rsrc {file.ResourceLength}→{file.ResourceCompressedLength} m{file.ResourceMethod}";
                Console.WriteLine($"{indent}{file.Name}  [{OsType(file.FileType)}/{OsType(file.FileCreator)}]  {forks}");
                break;
        }
    }

    private static string OsType(uint type) =>
        MacRoman.GetString([(byte)(type >> 24), (byte)(type >> 16), (byte)(type >> 8), (byte)type]);

    private static int CountFiles(IEnumerable<Sit5Entry> entries) =>
        entries.Sum(e => e is Sit5DirectoryEntry d ? CountFiles(d.Children) : 1);

    private static int CountDirectories(IEnumerable<Sit5Entry> entries) =>
        entries.Sum(e => e is Sit5DirectoryEntry d ? 1 + CountDirectories(d.Children) : 0);
}
