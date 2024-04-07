namespace Bounan.Downloader.Worker.Services;

internal partial class VideoMergingService
{
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Got video parts: {VideoParts}")]
        public static partial void GotVideoParts(ILogger logger, ICollection<Uri> videoParts);

        [LoggerMessage(LogLevel.Trace, "Downloaded part {PartNumber}/{TotalParts}")]
        public static partial void DownloadedPart(ILogger logger, int partNumber, int totalParts);

        [LoggerMessage(LogLevel.Trace, "Processed part {PartNumber}/{TotalParts}")]
        public static partial void ProcessedPart(ILogger logger, int partNumber, int totalParts);

        [LoggerMessage(LogLevel.Debug, "Finished merging")]
        public static partial void FinishedMerging(ILogger logger);
    }
}