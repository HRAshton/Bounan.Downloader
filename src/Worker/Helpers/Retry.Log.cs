namespace Bounan.Downloader.Worker.Helpers;

public static partial class Retry
{
    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "Attempt '{Attempt}' of '{MaxRetries}' failed. Retrying in '{DelayInMs}' ms.")]
        public static partial void RetryAttempt(ILogger logger, int attempt, int maxRetries, int delayInMs);
    }
}