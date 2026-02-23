using SpreadsheetFilterApp.Application.Abstractions.Time;

namespace SpreadsheetFilterApp.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
