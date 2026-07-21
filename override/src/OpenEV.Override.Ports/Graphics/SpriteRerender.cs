using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007a5e4 (EV Override-11.c lines 52264-52308): (re)render one sprite FRAME
// (the param is a SpriteFrames handle, NOT a render-list node). If the frame carries a
// rerender UPP (+0x26 RerenderUpp), dispatch it (saving/restoring port+GDevice). Then, if
// it has a CIcon id (+4), load that icon detached and plot/blit it into the frame's colour
// cell by render pass. Finally rebuild the mask region and free the icon.
public static class SpriteRerender
{
    public static void Run(int spriteFrame, int renderPass)
    {
        var f = SpriteFrames.At(spriteFrame);

        if (f.RerenderUpp != 0)
        {
            SaveCurrentPortAndDevice.Run(out int savedPort, out int savedDevice);
            DispatchSpriteByDepth.Run(spriteFrame);
            Misc.InvokeUppIntShort.Run(spriteFrame, (short)renderPass, f.RerenderUpp);
            SetPortAndDevice.Run(savedPort, savedDevice);
        }

        if (f.CIconId != 0 && Resource.LoadDetachedCIcon.Run(f.CIconId) != 0)
        {
            // UNREACHABLE: LoadDetachedCIcon is GetCIcon-backed (a 0-stub — cicn decoding
            // deferred), so this guard never admits. The Mac body plots/blits the detached
            // CIcon into the frame's colour cell per renderPass, then rebuilds the mask
            // region and disposes the icon — re-derive vs FUN_1007a378 when cicn lands.
            throw new System.NotSupportedException(
                "SpriteRerender: a cicn loaded (GetCIcon stub changed?) — re-derive the icon redraw body.");
        }
    }
}
