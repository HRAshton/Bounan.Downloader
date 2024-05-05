namespace Bounan.Downloader.Hls2TlgrUploader.Interfaces;

public interface IVideoUploadingService
{
    /// <summary>
    /// Downloads HLS parts, merges them into a single video and uploads it to Telegram.
    /// </summary>
    /// <param name="hlsParts">HLS parts to download and merge.</param>
    /// <param name="jpegThumbnailStreamTask">Task with a stream of a JPEG thumbnail.</param>
    /// <param name="caption">Caption for the video.</param>s
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task with the message ID of the uploaded video.</returns>
    Task<int> CopyToTelegramAsync(
        IList<Uri> hlsParts,
        Task<Stream> jpegThumbnailStreamTask,
        string caption,
        CancellationToken cancellationToken);
}