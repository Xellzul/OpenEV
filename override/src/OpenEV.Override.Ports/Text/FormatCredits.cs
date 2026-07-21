using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Text;

// Port of FUN_1005b4f8 (EV Override-11.c 37654-37694): draws a credit amount
// with a thousands "," and a millions ".NNM" suffix via NumToString + DrawString.
public static class FormatCredits
{
    public static void Run(int credits)
    {
        byte[] numBuf = new byte[260]; // NumToString scratch buffer

        if (credits < 1000)
        {
            MacToolbox.NumToString(credits, numBuf);
            MacToolbox.DrawString(numBuf);
        }
        else if (credits < 1000000)
        {
            MacToolbox.NumToString(credits / 1000, numBuf);
            MacToolbox.DrawString(numBuf);
            MacToolbox.DrawString(",");
            if (credits % 1000 < 100)
            {
                MacToolbox.DrawString("0");
            }
            if (credits % 1000 < 10)
            {
                MacToolbox.DrawString("0");
            }
            MacToolbox.NumToString(credits % 1000, numBuf);
            MacToolbox.DrawString(numBuf);
        }
        else
        {
            MacToolbox.NumToString(credits / 1000000, numBuf);
            MacToolbox.DrawString(numBuf);
            MacToolbox.DrawString(".");
            MacToolbox.NumToString((credits % 1000000) / 10000, numBuf);
            if ((credits % 1000000) / 10000 < 10)
            {
                MacToolbox.DrawString("0");
            }
            MacToolbox.DrawString(numBuf);
            MacToolbox.DrawString("M");
        }
    }
}
