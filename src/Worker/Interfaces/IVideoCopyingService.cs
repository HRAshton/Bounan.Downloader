namespace Bounan.Downloader.Worker.Interfaces;

public interface IVideoCopyingService
{
	public Task ProcessVideo(string message, CancellationToken cancellationToken);
}