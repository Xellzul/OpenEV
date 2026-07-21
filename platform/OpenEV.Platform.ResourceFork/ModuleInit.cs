using System.Runtime.CompilerServices;
using System.Text;

namespace OpenEV.Platform.ResourceFork;

internal static class ModuleInit
{
    /// <summary>Register CodePagesEncodingProvider once at module load so code page
    /// 10000 (Mac Roman, for <see cref="ForkResource.TypeCode"/> display) is available
    /// without per-call registration checks.</summary>
    [ModuleInitializer]
    internal static void Init() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}
