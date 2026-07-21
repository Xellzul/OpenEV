using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Decompile: EV Override-11.c lines 27698-27746.
// Loads PICT 8000 into the title-globals handle, allocates two regions,
// computes the 640×480 inner-arena rect centred in the main port, re-arms the
// button-reveal pulse, and copies the port rect into the backdrop rect.
//
// Fully managed: every toc-0x75xx cell this function writes now lives in
// TitleScreenGlobals (see its alias map) — the SAME field DrawClosedButtons
// and AnimateRowReveal read.
public static class InitTitleBackdrop
{
    public static void Run()
    {
        TitleScreenGlobals.Pict8000Handle = MacToolbox.GetPicture(8000);
        if (TitleScreenGlobals.Pict8000Handle == 0)
        {
            SetGamePortAndDevice.Run();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(GlobalState.PortRect);
            FatalGraphicsResourceExit.Run();
        }
        // KeyDownMask (0x8) — the decompile literal `8` is also, ambiguously, the raw
        // activateEvt event code; either reading yields a valid/no-op mask, so this is
        // NOT logged as a confirmed OGB bug (contrast the OGB-42 sites elsewhere).
        MacToolbox.FlushEvents(EventMask.KeyDownMask, 0);

        TitleScreenGlobals.RgnHandleA = MacToolbox.NewRgn();
        TitleScreenGlobals.RgnHandleB = MacToolbox.NewRgn();
        TitleScreenGlobals.SpeechEasterEggFlag = 0;
        GalaxyMapState.TradeKeyLock = 0;
        TitleScreenGlobals.ButtonRevealPulse = true;

        // Inner-arena rect: 640×480 centred in the ctx port rect, with the
        // PPC signed-/2 negative-odd correction kept verbatim.
        short[] arena = TitleScreenGlobals.InnerArenaRect;
        int sum = GlobalState.PortLeft + GlobalState.PortRight;
        arena[1] = (short)((sum >> 1) + (sum < 0 && (sum & 1) != 0 ? 1 : 0) + -320);
        sum = GlobalState.PortTop + GlobalState.PortBottom;
        arena[0] = (short)((sum >> 1) + (sum < 0 && (sum & 1) != 0 ? 1 : 0) + -240);
        arena[3] = (short)(arena[1] + 640);
        arena[2] = (short)(arena[0] + 480);

        // Backdrop rect = a copy of the ctx port rect (the decompile copies the
        // two packed ints), then centre PICT 8000's frame within it.
        short[] backdrop = TitleScreenGlobals.BackdropRect;
        GlobalState.PortRect.CopyTo(backdrop, 0);
        RectCenter.Run(TitleScreenGlobals.Pict8000Handle, backdrop);
    }
}
