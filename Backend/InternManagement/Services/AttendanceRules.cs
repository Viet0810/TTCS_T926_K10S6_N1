using System.Globalization;
namespace InternManagement.Services;

public static class AttendanceRules
{
    public static readonly TimeOnly Start = new(8,0);
    public static readonly TimeOnly End = new(17,30);
    private static TimeOnly? Parse(string? value) => TimeOnly.TryParseExact(value,
        ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
    public static string? CheckInStatus(string? value,TimeOnly? scheduledStart=null) => Parse(value) is {} time ? time <= (scheduledStart ?? Start) ? "ON_TIME" : "LATE" : null;
    public static string? CheckOutStatus(string? value,TimeOnly? scheduledEnd=null) => Parse(value) is {} time ? time >= (scheduledEnd ?? End) ? "COMPLETED" : "EARLY" : null;
    public static decimal Hours(string? checkIn,string? checkOut,TimeOnly? scheduledStart=null,TimeOnly? scheduledEnd=null)
    {
        if(Parse(checkIn) is not {} start || Parse(checkOut) is not {} end)return 0;
        var lower=scheduledStart??Start; var upper=scheduledEnd??End;
        var from=start < lower ? lower : start;
        var to=end > upper ? upper : end;
        return Math.Round(Math.Max(0,(decimal)(to.ToTimeSpan()-from.ToTimeSpan()).TotalHours),1);
    }
    public static string StoredStatus(string? checkIn,string? checkOut,TimeOnly? scheduledStart=null,TimeOnly? scheduledEnd=null) => CheckInStatus(checkIn,scheduledStart)=="LATE" ? "LATE" : CheckOutStatus(checkOut,scheduledEnd)=="EARLY" ? "EARLY" : "ON_TIME";
}
