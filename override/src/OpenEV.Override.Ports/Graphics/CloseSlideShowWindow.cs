using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Decompile: EV Override-11.c lines 47687-47724.
//
// Tear down the slideshow window's record state (SlideShowState) when `window`
// is the live slideshow window.
public static class CloseSlideShowWindow
{
    public static void Run(int window)
    {
        // The record behind *0x100819d0 is SlideShowState now (the
        // early-transcription extra-deref fix note lives there); the opener is unported so
        // the fields stay zeroed and this teardown is inert for window!=0.
        if (window == SlideShowState.Window)
        {
            SlideShowState.OpenFlag = 0;
            if (SlideShowState.Window != 0)
            {
                DisposeAllTextEditList.Run();
                MacToolbox.ShowHide(SlideShowState.Window, 0);
                CallSndUPP.Run(SlideShowState.Window);   // FUN_10073650 — trap-0xa88f dispose
                SlideShowState.Window = 0;
                SlideShowState.OpenWindowCount -= 1;
            }
            if (SlideShowState.SndChannel != 0)
            {
                InstallCallbackPtr.Run(SlideShowState.SndChannel, SlideShowState.CallbackArg);
                SlideShowState.SndChannel = 0;
            }
            if (SlideShowState.RoutineDescA != 0)
            {
                MacToolbox.DisposeRoutineDescriptor();     // decompile passes no recoverable arg
                SlideShowState.RoutineDescA = 0;
            }
            if (SlideShowState.RoutineDescB != 0)
            {
                MacToolbox.DisposeRoutineDescriptor();
                SlideShowState.RoutineDescB = 0;
            }
            if (SlideShowState.HandleB != 0)
            {
                MacToolbox.DisposeHandle();                // decompile passes no recoverable arg
                SlideShowState.HandleB = 0;
            }
            if (SlideShowState.HandleA != 0)
            {
                MacToolbox.DisposeHandle();
                SlideShowState.HandleA = 0;
            }
        }
        return;
    }
}
