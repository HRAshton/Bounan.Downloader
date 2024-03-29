using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Worker;

public partial class WorkerService(
	ILogger<WorkerService> logger,
	IOptions<ProcessingConfig> processingConfig,
	IAniManClient aniManClient,
	ISqsClient sqsClient,
	IVideoCopyingService videoCopyingService) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var semaphore = new SemaphoreSlim(processingConfig.Value.Threads, processingConfig.Value.Threads);

		Log.WorkerRunning(logger, DateTimeOffset.Now);

		var workers = Enumerable.Range(0, processingConfig.Value.Threads)
			.Select(i => RunWorkerInstance(semaphore, i, stoppingToken))
			.ToArray();

		await Task.WhenAll(workers);
	}

	private async Task RunWorkerInstance(SemaphoreSlim semaphore, int i, CancellationToken stoppingToken)
	{
		using var _ = Log.BeginScopeWorkerId(logger, i);

		while (!stoppingToken.IsCancellationRequested)
		{
			await semaphore.WaitAsync(stoppingToken);
			try
			{
				var message = await aniManClient.GetNextVideo(stoppingToken);

				if (message?.VideoKey is null)
				{
					await sqsClient.WaitForMessageAsync(stoppingToken);
					continue;
				}

				ArgumentNullException.ThrowIfNull(message.VideoKey);
				using var __ = Log.BeginScopeMsg(logger, message.Hash);
				await videoCopyingService.ProcessVideo(message.VideoKey, stoppingToken);
			}
			finally
			{
				semaphore.Release();
			}
		}
	}
}