using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007df64 (EV Override-11.c lines 54400-54479): blit one sprite FRAME into a
// buffer port (dstPort = port VALUE; 0 = the anim-scratch GWorld, with a dirty-rect enqueue
// first).
public static class BlitSpriteToBuffer
{
    public static void Run(int spriteFrame, int dstPort, int positionPacked, bool useDepthPort)
    {
        int defaultPort = Resource.ResourceGlobals.DefaultSpriteRenderer;
        short positionV = (short)(positionPacked >> 16);   // hi half of packed position
        short positionH = (short)positionPacked;           // lo half
        int savedPort = 0, savedDevice = 0;

        if (Resource.ResourceGlobals.ToolboxShimInitFlag == 0)
        {
            Misc.InitToolboxShimGlobals.Run();
        }
        if (spriteFrame != 0)
        {
            var f = SpriteFrames.At(spriteFrame);
            // Frame bounds offset by the packed position — enqueued as the dirty rect.
            var localRect = new short[4];
            localRect[0] = (short)(f.BoundsTop + positionV);
            localRect[1] = (short)(f.BoundsLeft + positionH);
            localRect[2] = (short)(f.BoundsBottom + positionV);
            localRect[3] = (short)(f.BoundsRight + positionH);
            int targetPort = defaultPort;
            if (useDepthPort)
            {
                switch (GlobalState.RenderMode)
                {
                    default:
                        break;
                    case 1:
                        targetPort = GlobalState.SpriteBlitterFrags[1];   // ctx+0xcc
                        break;
                    case 2:
                        targetPort = GlobalState.SpriteBlitterFrags[3];   // ctx+0xd4
                        break;
                    case 4:
                        targetPort = GlobalState.SpriteBlitterFrags[5];   // ctx+0xdc
                        break;
                    case 8:
                        targetPort = GlobalState.SpriteBlitterFrags[7];   // ctx+0xe4
                        break;
                    case 16:
                        targetPort = GlobalState.SpriteBlitterFrags[9];   // ctx+0xec
                        break;
                    case 32:
                        targetPort = GlobalState.SpriteBlitterFrags[11];  // ctx+0xf4
                        break;
                }
            }
            bool dstWasNull = dstPort == 0;
            if (dstWasNull)
            {
                EnqueueDirtyRect.Run(localRect);
                // The anim-scratch GWorld record (ctx+0x38); dstPort carries the port VALUE.
                dstPort = GlobalState.AnimScratchPort;
            }
            if (targetPort == defaultPort)
            {
                SaveCurrentPortAndDevice.Run(out savedPort, out savedDevice);
                // For the dstPort==0 fallback the record is the ctx+0x38/+0x3c anim-scratch
                // pair; a caller-passed dstPort is a bare port VALUE with no paired GDevice.
                if (dstWasNull)
                    SetPortAndDevice.Run(GlobalState.AnimScratchPort, GlobalState.AnimScratchGDevice);
                else
                    SetPortAndDevice.Run(dstPort, 0);
            }
            if (f.CustomDrawUpp == 0)
            {
                InvokeSpriteBlitUpp.Run(spriteFrame, 0, dstPort, 0, positionPacked, f.BoundsRight,
                                f.BoundsBottom, targetPort);
            }
            else
            {
                InvokeSpriteBlitUpp.Run(spriteFrame, 0, dstPort, 0, positionPacked, f.BoundsRight,
                                f.BoundsBottom, f.CustomDrawUpp);
            }
            if (targetPort == defaultPort)
            {
                SetPortAndDevice.Run(savedPort, savedDevice);
            }
        }
    }
}
