namespace Bounan.Downloader.Worker.Interfaces;

public interface ISqsService
{
	Task StartProcessing(Func<string, CancellationToken, Task> processMessageFn, CancellationToken cancellationToken);
}