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
    private readonly ProcessingConfig _processingConfig = processingConfig.Value;

    private ILogger Logger => logger;

    private IAniManClient AniManClient => aniManClient;

    private ISqsClient SqsClient => sqsClient;

    private IVideoCopyingService VideoCopyingService => videoCopyingService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var semaphore = new SemaphoreSlim(0, _processingConfig.Threads);

        Log.WorkerRunning(Logger, DateTimeOffset.Now);

        var workers = Enumerable.Range(0, _processingConfig.Threads)
            .Select(i => RunWorkerInstance(semaphore, i, stoppingToken))
            .Concat([ SqsWatcher(semaphore, stoppingToken) ])
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task SqsWatcher(SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SqsClient.WaitForMessageAsync(stoppingToken);
            semaphore.Release(_processingConfig.Threads);
        }
    }

    private async Task RunWorkerInstance(SemaphoreSlim semaphore, int i, CancellationToken stoppingToken)
    {
        using var _ = Log.BeginScopeWorkerId(Logger, i);

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await AniManClient.GetNextVideo(stoppingToken);
            if (message?.VideoKey is null)
            {
                Log.WaitingForMessage(Logger);
                await semaphore.WaitAsync(stoppingToken);
                Log.WorkerReleased(Logger);
                continue;
            }

            ArgumentNullException.ThrowIfNull(message.VideoKey);
            using var __ = Log.BeginScopeMsg(Logger, message.Hash);
            await VideoCopyingService.ProcessVideo(message.VideoKey, stoppingToken);
        }
    }
}