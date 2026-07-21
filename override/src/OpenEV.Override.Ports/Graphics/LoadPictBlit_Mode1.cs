namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100792c4 (EV Override-11.c lines 51561-51574).
//
// MANAGED: takes the stage Rect as packed {top,left}/{bottom,right} values (the original
// took a Rect pointer and read the two ints straight out of it).
public static class LoadPictBlit_Mode1
{
    public static void Run(short pictId, int rectTopLeft, int rectBotRight)
    {
        DrawPictResource.Run(pictId, rectTopLeft, rectBotRight, 1);
    }
}
