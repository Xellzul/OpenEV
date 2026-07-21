using OpenEV.Override.Ports.EvoMath;

namespace OpenEV.Override.Ports.Sound;

// FUN_1005eb04 (EV Override-11.c 39385-39438) — positional stereo SndPlay: both
// channels 128 (full) within 1000px of the listener on each axis, 64 beyond;
// pan by dropping the far channel to 96 beyond ±400px horizontally. The
// listener args are ALWAYS the player record at every original call site.
// channelMask (2/3/-1 at the sites) is accepted but never touched — no stack
// slot, never read — kept for signature fidelity.
public static class PlayPositionalSound
{
    public static int Run(int channelMask, int sndHandle, short priority,
                          float srcX, float srcY, float listenerX, float listenerY)
    {
        float dx = srcX - listenerX;
        // Faithful quirk: fabs(dy) is computed under an always-true guard
        // (fabs(dx) <= 3.46e18, the 3*2^60 float @0x10082260 — loaded via `lfs`,
        // not `lfd` like the four dbl_ constants below; exact in either precision)
        // and DISCARDED.
        if (EvMath.FloatAbs(dx) <= 3.4587645138205409e18)
        {
            EvMath.FloatAbs(srcY - listenerY);
        }

        short left = 128, right = 128;
        if (1000f /* @0x100822c8 */ < EvMath.FloatAbs(dx) ||
            1000f < EvMath.FloatAbs(srcY - listenerY))
        {
            left = 64;
            right = 64;
        }
        if (left == 128)
        {
            if (dx < -400.0 /* @0x10082258 */)
            {
                right = 96;
            }
            if (400.0 /* @0x10082250 */ < dx)
            {
                left = 96;
            }
        }
        else if (left == 128)
        {
            // DEAD in the ORIGINAL: the else-if repeats the condition above, so
            // the ±1000px hard pan to 32 (cells 0x10082240/0x10082248) never runs.
            if (dx < -1000.0)
            {
                right = 32;
            }
            if (1000.0 < dx)
            {
                left = 32;
            }
        }
        SndPlay.Run(sndHandle, priority, left, right);
        return 1;
    }
}
