using System;

namespace OpenEV.Platform.Imaging;

public static class DecodeDiagnostics
{
    public static bool Verbose { get; } =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVO_DECODE_VERBOSE"));

    public static void Log(string? tag, string message)
    {
        if (!Verbose) return;
        if (tag is null) Console.WriteLine($"  [DIAG] {message}");
        else Console.WriteLine($"  [DIAG] {tag}: {message}");
    }
}
