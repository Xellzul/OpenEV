namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;

// Port of FUN_10067b50 (EV Override-11.c 43104-43114) — while a chatter flash countdown is running,
// re-enqueue the last chatter message (the replay buffer) so it survives the play-area redraw.
// messageFlag is forwarded to EnqueueChatterEvent's final arg (0 from most callers, 1 from the scroll path).
public static class DispatchPendingChatter
{
    public static void Run(byte messageFlag)
    {
        if (0 < WorldState.FlashChatterCountdown)
            EnqueueChatterEvent.Run(ChatterState.LastMessage, (short)WorldState.FlashChatterCountdown, 0, 12, UiColors.ChatterText, 0, messageFlag);
    }
}
