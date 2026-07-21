using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007a950 (EV Override-11.c lines 52384-52423): copy the depth-selected
// PR/PM sprite renderers from the per-depth SpriteBlitterFrags[] into CurrentDepthRenderer
// (+0xf8) / CurrentDepthRendererPM (+0xfc). Frag index = depth slot d*2 (PR) / d*2+1 (PM)
// for depths 1/2/4/8/16/32; the default (RenderMode 0) uses the plain sprite renderers.
public static class SelectSpriteRenderersByDepth
{
    public static void Run()
    {
        var g = GlobalState.SpriteBlitterFrags;
        int pr, pm;
        switch (GlobalState.RenderMode)
        {
            default: pr = Resource.ResourceGlobals.SpriteRendererVariant; pm = Resource.ResourceGlobals.DefaultSpriteRenderer; break;
            case 1: pr = g[0]; pm = g[1]; break;
            case 2: pr = g[2]; pm = g[3]; break;
            case 4: pr = g[4]; pm = g[5]; break;
            case 8: pr = g[6]; pm = g[7]; break;
            case 16: pr = g[8]; pm = g[9]; break;
            case 32: pr = g[10]; pm = g[11]; break;
        }
        GlobalState.CurrentDepthRenderer = pr;   // ctx+0xf8
        GlobalState.CurrentDepthRendererPM = pm;   // ctx+0xfc
    }
}
