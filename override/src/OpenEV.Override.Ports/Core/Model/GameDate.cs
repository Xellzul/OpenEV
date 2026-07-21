using System;

namespace OpenEV.Override.Ports.Core.Model;

// A game calendar date — the managed form of the Mac 3-short date struct
// (+0 year, +2 month, +4 day). Replaces the int-pointer date APIs.
// Ports: FUN_1005c2cc (AdvanceOneDay) + FUN_1004a4c4 (AdvanceDays).
// Name source: src/OpenEvo.MacOS/GameDate.cs.
public struct GameDate
{
    public short Year;
    public short Month;
    public short Day;

    public GameDate(short year, short month, short day)
    {
        Year = year;
        Month = month;
        Day = day;
    }

    // The global game clock — MANAGED. Formerly the Mac DateTimeRec behind the
    // PTR slot 0x10080dec (toc-0x7874, target 0x100e0208), filled
    // by the GetTime trap and bumped +250 years at new-pilot init. Only the
    // year/month/day shorts (+0/+2/+4) are ever read by the game.
    private static GameDate _current;
    public static GameDate Current
    {
        get => _current;
        set => _current = value;
    }

    // The GetTime trap on the global record: fill the date from the host clock
    // (the original wrote the real-world date; the +250-year offset is applied
    // by the callers, exactly as the original did).
    public static void SetCurrentToHostClock()
    {
        var now = DateTime.Now;
        _current = new GameDate((short)now.Year, (short)now.Month, (short)now.Day);
    }

    // FUN_1005c2cc — advance this date by one calendar day, rolling month/year over
    // (February gets 29/28 days by the year-divisible-by-4 leap test).
    public void AdvanceOneDay()
    {
        short daysInMonth = 0;
        switch (Month)
        {
            case 1: case 3: case 5: case 7: case 8: case 10: case 12: daysInMonth = 31; break;
            case 4: case 6: case 9: case 11: daysInMonth = 30; break;
            case 2:
                // leap = year divisible by 4 (the decompile's exact (year>>2)*4 == year test).
                uint year = (uint)Year;
                uint quad = (uint)(((int)year >> 2) + (((int)year < 0 && (year & 3) != 0) ? 1 : 0)) * 4;
                daysInMonth = year == quad ? (short)29 : (short)28;
                break;
        }
        Day = (short)(Day + 1);
        if (Day <= daysInMonth)
            return;
        Day = 1;
        Month = (short)(Month + 1);
        if (Month < 13)
            return;
        Month = 1;
        Year = (short)(Year + 1);
    }

    // FUN_1004a4c4 — the current global date advanced by `dayCount` days. Returns null
    // when dayCount <= 0 (the original left its output struct untouched in that case).
    public static GameDate? AdvanceDays(short dayCount)
    {
        if (dayCount <= 0)
            return null;
        GameDate d = Current;
        for (short i = 0; i < dayCount; i = (short)(i + 1))
            d.AdvanceOneDay();
        return d;
    }

    // Advance the global game clock by one day (FUN_1005c438's daily tick).
    public static void AdvanceCurrentOneDay()
    {
        GameDate d = Current;
        d.AdvanceOneDay();
        Current = d;
    }
}
