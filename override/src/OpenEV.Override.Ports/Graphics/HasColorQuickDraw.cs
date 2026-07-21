using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10071628 (decompile 46549-46565) — is Color QuickDraw version 2.0 or later present?
public static class HasColorQuickDraw
{
    public static bool Run()
    {
        // Gestalt selector 'qd  ' (gestaltQuickdrawVersion); response[0] is the packed
        // Color QuickDraw version word (0x0200 = version 2.0).
        short gestaltErr = MacToolbox.Gestalt(0x71642020, out uint[] gestaltResponse);
        return gestaltErr == 0 && gestaltResponse[0] > 0x200;
    }
}
