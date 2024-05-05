using System.IO.Pipelines;

namespace Bounan.Downloader.Hls2TlgrUploader.Interfaces;

internal interface IVideoMergingService
{
    Task DownloadToPipeAsync(IList<Uri> hlsParts, PipeWriter pipeWriter, CancellationToken cancellationToken);
}