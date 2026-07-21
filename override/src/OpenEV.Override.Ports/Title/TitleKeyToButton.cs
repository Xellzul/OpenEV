using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// FUN_10043900 (EV Override-11.c lines 28012-28068).
// Maps a title-screen keypress to a synthetic click inside the matching button
// rect (n=New Pilot, e=Enter Ship, o=Open Pilot, p=Set Prefs, q=Quit, a=About;
// x runs the victory animation directly). The Mac built a stack EventRecord
// whose 'where' Point = (rect.top+1, rect.left+1); DispatchTitleEvent reads
// only that Point, so the managed form passes the packed point directly.
public static class TitleKeyToButton
{
    // eventMessage (param_2, the raw EventRecord.message) is dead in the original too — FUN_10043900
    // never reads it (confirmed against the ASM: r4/param_2 is never stored). Kept in the signature
    // to mirror the caller's event fields faithfully.
    public static void Run(byte keyCode, int eventMessage, short modifiers)
    {
        short[][] buttons = TitleScreenGlobals.ButtonRects;
        int packedPoint = 0x10001;   // default: (1,1) — hits no button

        byte mappedKey = (byte)LookupKeyTableUnshifted.Run(keyCode);
        switch (mappedKey)
        {
            case (byte)'n':
                packedPoint = PointInside(buttons[0]);
                break;
            case (byte)'o':
                // Only when neither chord key (V / N) is held.
                if (Keymap.TestLiveKeymapBit(MacKeycode.V) == 0 &&
                    Keymap.TestLiveKeymapBit(MacKeycode.N) == 0)
                {
                    packedPoint = PointInside(buttons[2]);
                }
                break;
            case (byte)'q':
                packedPoint = PointInside(buttons[4]);
                break;
            case (byte)'e':
                packedPoint = PointInside(buttons[1]);
                break;
            case (byte)'p':
                packedPoint = PointInside(buttons[3]);
                break;
            case (byte)'a':
                packedPoint = PointInside(buttons[5]);
                break;
        }

        if (mappedKey == (byte)'x')
        {
            PlayVictoryAnimation.Run();
        }
        else
        {
            DispatchTitleEvent.Run(packedPoint);
        }
    }

    // Packed Point (v<<16 | h) one pixel inside the rect's top-left corner.
    private static int PointInside(short[] rect)
      => (((rect[0] + 1) & 0xffff) << 16) | ((rect[1] + 1) & 0xffff);
}
