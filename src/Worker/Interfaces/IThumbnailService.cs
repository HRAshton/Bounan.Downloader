using Bounan.Common;

namespace Bounan.Downloader.Worker.Interfaces;

public interface IThumbnailService
{
    Task<Stream> GetThumbnailJpegStreamAsync(
        Uri originalThumbnailUrl,
        IVideoKey videoKey,
        CancellationToken cancellationToken);
}