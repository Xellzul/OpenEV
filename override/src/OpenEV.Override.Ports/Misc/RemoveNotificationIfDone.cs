using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_100734a4 (EV Override-11.c lines 47669-47686) — dismiss the slideshow's
// pending Notification Manager alert when the slideshow window isn't open;
// otherwise tear the window down via FUN_100734f4 (CloseSlideShowWindow).
public static class RemoveNotificationIfDone
{
    public static int Run()
    {
        if (SlideShowState.Window == 0)
        {
            if (SlideShowState.NmPosted != 0)
            {
                MacToolbox.NMRemove(0);   // NO-OP: NMRemove is a no-op stub; the real arg would be NMRec at rec+4
            }
        }
        else
        {
            // The decompile's `else { FUN_100734f4(); }` (line 47678) shows a no-arg call, but the
            // ASM proves r3 still held Window from the preceding compare, and the callee self-guards
            // `param_1 == Window` — passing anything else (e.g. a hardcoded 0) fails that guard and
            // leaks the window/sound-channel/handles. (Dead today: the slideshow opener is unported,
            // so Window never leaves 0.)
            CloseSlideShowWindow.Run(SlideShowState.Window);
        }
        return 0;
    }
}
