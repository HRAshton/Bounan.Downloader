namespace Bounan.Downloader.Worker.Interfaces;

public interface ISqsClient
{
	Task WaitForMessageAsync(CancellationToken cancellationToken);
}