namespace Bounan.Downloader.Worker.Helpers;

public static partial class Retry
{
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Warning,
            "Attempt '{Attempt}' of '{MaxRetries}' failed. Retrying in '{DelayInMs}' ms."
            + " Message: {ErrorMessage}. StackTrace: {StackTrace}. ")]
        public static partial void RetryAttempt(
            ILogger logger,
            int attempt,
            int maxRetries,
            int delayInMs,
            string errorMessage,
            string? stackTrace);
    }
}
