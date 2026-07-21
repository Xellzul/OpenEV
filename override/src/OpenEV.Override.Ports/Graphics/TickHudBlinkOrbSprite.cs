using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10020474 (EV Override-11.c lines 14359-14391): tick the blinking HUD orb sprite
// (the right-edge target/comm indicator). While its countdown is positive it alternates the
// lit/dim orb frames each step, parks itself at the right edge of the play area, and decrements
// every 5th game-frame. When the countdown runs out the orb sprite is cleared.
public static class TickHudBlinkOrbSprite
{
    public static void Run(int spriteNode)
    {
        var n = SpriteNodes.At(spriteNode);
        short count = WorldState.HudBlinkCountdown;
        if (count < 1)
        {
            n.SpritePtr = 0;
            return;
        }

        // Alternate the orb frame: even count = lit, odd = dim.
        n.SpritePtr = SpriteFrameTables.HudOrbFrames[(count & 1) == 0 ? 0 : 1];

        short orbWidth = (short)MacRectWidth.Run(n.SpritePtr);
        // Right-aligned HUD position = 2 * cameraCentreX - orbWidth.
        n.PosX = (short)(WorldFlags.CameraCentreX * 2 - orbWidth);
        n.PosY = 0;

        // Every 5th game-frame tick, count the orb down.
        int frame = WorldState.GameFrameTickCounter;
        if (frame == frame / 5 * 5)
            WorldState.HudBlinkCountdown = (short)(count - 1);
    }
}
