using System.Net;

namespace Bounan.Downloader.Worker.Clients;

public partial class AniManClient
{
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to get video info: {ErrorMessage}")]
        public static partial void FailedToGetVideoInfo(ILogger logger, HttpStatusCode errorMessage);

        [LoggerMessage(LogLevel.Error, "Failed to get video info: {Exception}")]
        public static partial void FailedToGetVideoInfo(ILogger logger, Exception exception);

        [LoggerMessage(LogLevel.Error, "Failed to send result: {HttpStatusCode}")]
        public static partial void FailedToSendResult(ILogger logger, HttpStatusCode httpStatusCode);

        [LoggerMessage(LogLevel.Error, "Failed to send result: {Exception}")]
        public static partial void FailedToSendResult(ILogger logger, Exception exception);
    }
}