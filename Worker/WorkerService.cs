using Bounan.Downloader.Worker.Interfaces;

namespace Bounan.Downloader.Worker;

public class WorkerService(
	ILogger<WorkerService> logger,
	ISqsService sqsService,
	IVideoCopyingService videoCopyingService) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
		}

		await sqsService.StartProcessing(videoCopyingService.ProcessVideo, stoppingToken);
	}
}