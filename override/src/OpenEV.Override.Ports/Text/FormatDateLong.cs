namespace OpenEV.Override.Ports.Text;

// Port of FUN_1005db98 (EV Override-11.c 38902-38981): the abbreviated game date,
// e.g. "June 12th, 2176". The original took the date record's four ints plus an
// out-string; callers pass the three shorts and take the built string.
public static class FormatDateLong
{
    public static string Run(short year, short month, short day)
    {
        string s = "";
        // Twelve independent ifs, no else-chain — kept as the original: exactly one fires.
        if (month == 1) s += "Jan.";
        if (month == 2) s += "Feb.";
        if (month == 3) s += "Mar.";
        if (month == 4) s += "Apr.";
        if (month == 5) s += "May";
        if (month == 6) s += "June";
        if (month == 7) s += "July";
        if (month == 8) s += "Aug.";
        if (month == 9) s += "Sept.";
        if (month == 10) s += "Oct.";
        if (month == 11) s += "Nov.";
        if (month == 12) s += "Dec.";
        s += " ";
        s += day.ToString();
        string suffix = "th";
        if (day % 10 == 1) suffix = "st";
        if (day % 10 == 2) suffix = "nd";
        if (day % 10 == 3) suffix = "rd";
        if (10 < day && day < 14) suffix = "th";   // 11th/12th/13th stay "th"
        s += suffix;
        s += ", ";
        s += year.ToString();
        return s;
    }
}
