namespace OpenEV.Override.Ports.Text;

// Port of FUN_1005da9c (EV Override-11.c 38869-38901): the numeric game date
// "M/D/YY" (year mod 100, no zero-padding — matches NumToString).
public static class FormatDateNumeric
{
    public static string Format(short year, short month, short day)
      => $"{month}/{day}/{year % 100}";
}