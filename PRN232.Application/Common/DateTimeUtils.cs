namespace PRN232.Application.Common;

public static class DateTimeUtils
{
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateOnly ToDateOnly(DateTime value) => DateOnly.FromDateTime(value);
}
