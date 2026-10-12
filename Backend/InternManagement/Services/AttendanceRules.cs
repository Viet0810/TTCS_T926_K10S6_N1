using System.Globalization;
namespace InternManagement.Services;

public static class AttendanceRules
{
    public static readonly TimeOnly Start = new(8,0);
    public static readonly TimeOnly End = new(17,30);
    private static TimeOnly? Parse(string? value) => TimeOnly.TryParseExact(value,
        ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
    public static string? CheckInStatus(string? value) => Parse(value) is {} time ? time <= Start ? "ON_TIME" : "LATE" : null;
    public static string? CheckOutStatus(string? value) => Parse(value) is {} time ? time >= End ? "COMPLETED" : "EARLY" : null;
    public static decimal Hours(string? checkIn,string? checkOut)
    {
        if(Parse(checkIn) is not {} start || Parse(checkOut) is not {} end)return 0;
        var from=start < Start ? Start : start;
        var to=end > End ? End : end;
        return Math.Round(Math.Max(0,(decimal)(to.ToTimeSpan()-from.ToTimeSpan()).TotalHours),1);
    }
    public static string StoredStatus(string? checkIn,string? checkOut) => CheckInStatus(checkIn)=="LATE" ? "LATE" : CheckOutStatus(checkOut)=="EARLY" ? "EARLY" : "ON_TIME";
}
