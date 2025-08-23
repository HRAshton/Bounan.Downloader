using Bounan.Common;

namespace Bounan.Downloader.Worker.Services;

internal partial class ThumbnailService
{
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Got anime name for {VideoKey}: {AnimeName} ({Dub})")]
        public static partial void GotAnimeName(
            ILogger<ThumbnailService> logger,
            IVideoKey videoKey,
            string animeName,
            string dub);

        [LoggerMessage(LogLevel.Information, "Got original image with size {Width}x{Height}")]
        public static partial void GotOriginalImage(ILogger<ThumbnailService> logger, int width, int height);

        [LoggerMessage(LogLevel.Information, "Created watermark with size {Width}x{Height}")]
        public static partial void CreatedWatermark(ILogger<ThumbnailService> logger, int width, int height);

        [LoggerMessage(LogLevel.Information, "Drawn watermark")]
        public static partial void DrawnWatermark(ILogger<ThumbnailService> logger);

        [LoggerMessage(LogLevel.Information, "Saved thumbnail with size {Length}")]
        public static partial void SavedThumbnail(ILogger<ThumbnailService> logger, long length);

        [LoggerMessage(LogLevel.Warning, "Different anime names: {AnimeNames}")]
        public static partial void DifferentAnimeNames(ILogger<ThumbnailService> logger, string[] animeNames);

        [LoggerMessage(LogLevel.Information, "Original image used as thumbnail")]
        public static partial void OriginalImageUsedAsThumbnail(ILogger<ThumbnailService> logger);
    }
}