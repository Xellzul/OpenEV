namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10079300 (EV Override-11.c lines 51575-51588).
//
// MANAGED: takes the stage Rect as packed {top,left}/{bottom,right} values (the original
// took a Rect pointer and read the two ints straight out of it).
public static class LoadPictBlit_Mode2
{
    public static void Run(short pictId, int rectTopLeft, int rectBotRight)
    {
        DrawPictResource.Run(pictId, rectTopLeft, rectBotRight, 2);
    }
}
