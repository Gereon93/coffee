using Microsoft.Extensions.Logging;

namespace CoffeeTest.Helpers;

/// <summary>
/// Captures log entries so tests can assert on what was logged.
/// The watchdog's alarm channel *is* an Error-level log entry (Sentry promotes
/// those to GlitchTip events), so asserting on the log is asserting on the alarm.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    public IReadOnlyList<string> MessagesAt(LogLevel level) =>
        Entries.Where(e => e.Level == level).Select(e => e.Message).ToList();
}
