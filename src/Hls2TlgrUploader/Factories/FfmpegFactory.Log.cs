using Microsoft.Extensions.Logging;

namespace Bounan.Downloader.Hls2TlgrUploader.Factories;

internal partial class FfmpegFactory
{
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Ffmpeg service created")]
        public static partial void FfmpegServiceCreated(ILogger logger);
    }
}