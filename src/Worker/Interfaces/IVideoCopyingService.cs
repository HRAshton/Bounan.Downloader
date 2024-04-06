using Bounan.Common.Models;

namespace Bounan.Downloader.Worker.Interfaces;

public interface IVideoCopyingService
{
    public Task ProcessVideo(IVideoKey videoKey, CancellationToken cancellationToken);
}