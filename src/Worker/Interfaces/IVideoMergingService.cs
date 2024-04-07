using System.IO.Pipelines;

namespace Bounan.Downloader.Worker.Interfaces;

public interface IVideoMergingService
{
    Task DownloadToPipeAsync(Uri playlistUrl, PipeWriter pipeWriter, CancellationToken cancellationToken);
}