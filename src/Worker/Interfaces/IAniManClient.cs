using Bounan.Common;

namespace Bounan.Downloader.Worker.Interfaces;

public interface IAniManClient
{
    Task<DownloaderResponse?> GetNextVideo(CancellationToken cancellationToken);

    Task SendResult(DownloaderResultRequest result, CancellationToken cancellationToken);
}