using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007e120 (EV Override-11.c lines 54480-54549). (An earlier port-added
// title-orb fast path — frame-index sentinels 1..4 stamping unmasked 25×25 PICT-8001
// cells — is GONE: it drew a SQUARE orb where the original CopyMasks spïn 900's
// 32×32 cell through its ~25px disc mask. HoverOrbDrawErase now passes the real
// toc+0x6c40 frame records, which take the sprite-record path like everything else.)
public static class BlitSpriteToWindow
{
    public static void Run(int spriteNode, int positionPacked, bool useDepthPort)
    {
        int savedPort = 0, savedDevice = 0;
        int defaultPort = Resource.ResourceGlobals.DefaultSpriteRenderer;
        short positionV = (short)(positionPacked >> 16);
        short positionH = (short)positionPacked;
        if (Resource.ResourceGlobals.ToolboxShimInitFlag == 0)
        {
            Misc.InitToolboxShimGlobals.Run();
        }
        if (spriteNode != 0)
        {
            // spriteNode (past the orb sentinels) is a sprite FRAME record.
            var f = SpriteFrames.At(spriteNode);
            // Scroll-relative position (ctx+0x52 ScrollOffsetX / ctx+0x56 ScrollOffsetY):
            // computed but never read — faithful dead locals (original local_14/local_16).
            short relH = (short)(positionH - (short)GlobalState.ScrollOffsetX);
            short relV = (short)(positionV - (short)GlobalState.ScrollOffsetY);
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
            if (targetPort == defaultPort)
            {
                SaveCurrentPortAndDevice.Run(out savedPort, out savedDevice);
                SetGamePortAndDevice.Run();
            }
            if (f.CustomDrawUpp == 0)
            {
                InvokeSpriteBlitUpp.Run(spriteNode, 0, GlobalState.ActivePortPixmap, 0, positionPacked, f.BoundsRight,
                                f.BoundsBottom, targetPort);
            }
            else
            {
                InvokeSpriteBlitUpp.Run(spriteNode, 0, GlobalState.ActivePortPixmap, 0, positionPacked, f.BoundsRight,
                                f.BoundsBottom, f.CustomDrawUpp);
            }
            if (targetPort == defaultPort)
            {
                SetPortAndDevice.Run(savedPort, savedDevice);
            }
        }
    }
}
