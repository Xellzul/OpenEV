namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 47582-47599.
public static class EscalationLevel
{
    public static int Run()
    {
        int level = 0;
        GetDaysSinceInstall.Run(out int installDays);
        GetInstallHours.Run(out int installHours);
        if (30 < installDays && 6 < installHours)
        {
            level = 1;
        }
        if (60 < installDays && 12 < installHours)
        {
            level++;
        }
        return level;
    }
}
