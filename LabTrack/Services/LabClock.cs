namespace LabTrack.Services;

public class LabClock
{
    private static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
    public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Lima), DateTimeKind.Unspecified);
}
