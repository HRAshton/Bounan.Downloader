using Bounan.Common.Models;
using Bounan.Downloader.Worker.Models;

namespace Bounan.Downloader.Worker.Services;

public partial class VideoCopyingService
{
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Received video key: {VideoKey}")]
        public static partial void ReceivedVideoKey(ILogger<VideoCopyingService> logger, IVideoKey videoKey);

        [LoggerMessage(LogLevel.Debug, "Processing video {SignedUrl}")]
        public static partial void ProcessingVideo(ILogger logger, Uri signedUrl);

        [LoggerMessage(LogLevel.Debug, "Got video info: {VideoInfo}")]
        public static partial void GotVideoInfo(ILogger logger, VideoInfo videoInfo);

        [LoggerMessage(LogLevel.Information, "Video uploaded with file id: {FileId}")]
        public static partial void VideoUploaded(ILogger logger, string fileId);

        [LoggerMessage(LogLevel.Debug, "Got playlists and thumbnail: {Playlists}; {Thumbnail}")]
        public static partial void GotPlaylistsAndThumbnail(
            ILogger logger,
            Dictionary<string, Uri> playlists,
            Uri thumbnail);

        [LoggerMessage(LogLevel.Debug, "Processing playlist: {Playlist}")]
        public static partial void ProcessingPlaylist(ILogger logger, Uri playlist);

        [LoggerMessage(LogLevel.Debug, "Got video parts: {VideoParts}")]
        public static partial void GotVideoParts(ILogger logger, ICollection<Uri> videoParts);

        [LoggerMessage(LogLevel.Trace, "Downloaded part {PartNumber}/{TotalParts}")]
        public static partial void DownloadedPart(ILogger logger, int partNumber, int totalParts);

        [LoggerMessage(LogLevel.Trace, "Processed part {PartNumber}/{TotalParts}")]
        public static partial void ProcessedPart(ILogger logger, int partNumber, int totalParts);

        [LoggerMessage(LogLevel.Debug, "Video uploaded")]
        public static partial void VideoUploaded(ILogger logger);

        [LoggerMessage(LogLevel.Error, "Error processing video: {Exception}")]
        public static partial void ErrorProcessingVideo(ILogger logger, Exception exception);

        [LoggerMessage(LogLevel.Information, "Result sent: {Result}")]
        public static partial void ResultSent(ILogger logger, IDwnResultNotification result);
    }
}