using System.Runtime.CompilerServices;
using System.Text;

namespace OpenEV.Platform.EvoData;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        // Required so that Encoding.GetEncoding("Windows-1252") works on .NET Core/8.
        // Mac resource fork bytes are MacRoman; we decode through Windows-1252 to match
        // how the unpacked .rsrc sidecars were dumped on disk.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
