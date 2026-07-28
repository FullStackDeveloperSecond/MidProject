using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MidProject.Services;

public sealed class NotificationAuditFileLoggerProvider : ILoggerProvider
{
    private const string FilePrefix = "notifications-";
    private const string FileExtension = ".jsonl";

    private readonly string _directory;
    private readonly int _retentionDays;
    private readonly long _maxFileBytes;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _taipeiTimeZone;
    private readonly object _writeLock = new();
    private DateOnly? _lastCleanupDate;

    public NotificationAuditFileLoggerProvider(
        string directory,
        int retentionDays,
        long maxFileBytes,
        TimeProvider timeProvider)
    {
        _directory = directory;
        _retentionDays = Math.Clamp(retentionDays, 1, 365);
        _maxFileBytes = Math.Clamp(maxFileBytes, 1_048_576, 104_857_600);
        _timeProvider = timeProvider;
        _taipeiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new NotificationAuditFileLogger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private static bool IsNotificationCategory(string categoryName)
    {
        return categoryName.Contains("Notification", StringComparison.Ordinal);
    }

    private bool IsEnabled(string categoryName, LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information &&
               IsNotificationCategory(categoryName);
    }

    private void Write(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (!IsEnabled(categoryName, logLevel))
        {
            return;
        }

        var taipeiNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _taipeiTimeZone);
        var logDate = DateOnly.FromDateTime(taipeiNow.DateTime);
        var payload = JsonSerializer.Serialize(new
        {
            Timestamp = taipeiNow,
            Level = logLevel.ToString(),
            Category = categoryName,
            EventID = eventId.Id,
            Message = message,
            ExceptionType = exception?.GetType().Name
        });
        var bytes = Encoding.UTF8.GetByteCount(payload) + Environment.NewLine.Length;

        lock (_writeLock)
        {
            Directory.CreateDirectory(_directory);
            if (_lastCleanupDate != logDate)
            {
                DeleteExpiredFiles(logDate);
                _lastCleanupDate = logDate;
            }

            var filePath = ResolveWritableFile(logDate, bytes);
            File.AppendAllText(
                filePath,
                payload + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private string ResolveWritableFile(DateOnly logDate, int pendingBytes)
    {
        var dateToken = logDate.ToString("yyyyMMdd");
        for (var segment = 1; segment < 10_000; segment++)
        {
            var filePath = Path.Combine(
                _directory,
                $"{FilePrefix}{dateToken}-{segment:00}{FileExtension}");
            var currentLength = File.Exists(filePath)
                ? new FileInfo(filePath).Length
                : 0;
            if (currentLength + pendingBytes <= _maxFileBytes)
            {
                return filePath;
            }
        }

        throw new IOException("Notification audit log segment limit was reached.");
    }

    private void DeleteExpiredFiles(DateOnly currentDate)
    {
        var oldestRetainedDate = currentDate.AddDays(-(_retentionDays - 1));
        foreach (var path in Directory.EnumerateFiles(
                     _directory,
                     $"{FilePrefix}*{FileExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Length < FilePrefix.Length + 8 ||
                !DateOnly.TryParseExact(
                    fileName.Substring(FilePrefix.Length, 8),
                    "yyyyMMdd",
                    out var fileDate) ||
                fileDate >= oldestRetainedDate)
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A locked historical segment is retried on the next cleanup cycle.
            }
            catch (UnauthorizedAccessException)
            {
                // Startup must not fail solely because an operator changed log permissions.
            }
        }
    }

    private sealed class NotificationAuditFileLogger(
        NotificationAuditFileLoggerProvider provider,
        string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return provider.IsEnabled(categoryName, logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.Write(
                categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                exception);
        }
    }
}
