namespace Bounan.Downloader.Worker.Clients;

public partial class SqsClient
{
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Waiting for a message")]
        public static partial void WaitingForMessage(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Running video processing")]
        public static partial void RunningVideoProcessing(ILogger logger);

        [LoggerMessage(LogLevel.Error, "Failed to receive message: {ErrorMessage}")]
        public static partial void FailedToReceiveMessage(ILogger logger, string errorMessage);
    }
}