namespace OpenEV.Override.Ports.Text;

// Port of FUN_1005de74 (EV Override-11.c 38982-39063): the full-month game date,
// e.g. "June 11th, 2176".
public static class FormatDateLongFull
{
    // Faithfulness: month 9 is "Sept." (abbreviated) even in this "full" formatter —
    // the game data has no "September" string; FUN_1005de74 (line 39024) appends the
    // same "Sept." the abbreviated FUN_1005db98 uses. Do NOT "correct" it to September.
    private static readonly string[] MonthNames = {
  "January", "February", "March", "April", "May", "June",
  "July", "August", "Sept.", "October", "November", "December" };

    public static string Format(short year, short month, short day)
    {
        string result = "";
        if (1 <= month && month <= 12)
        {
            result += MonthNames[month - 1];
        }
        result += " " + day;
        string suffix = "th";
        if (day % 10 == 1) suffix = "st";
        if (day % 10 == 2) suffix = "nd";
        if (day % 10 == 3) suffix = "rd";
        if (10 < day && day < 14) suffix = "th";   // 11th/12th/13th stay "th"
        return result + suffix + ", " + year;
    }
}