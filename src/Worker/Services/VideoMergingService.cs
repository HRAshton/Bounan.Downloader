using System.IO.Pipelines;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Helpers;
using Bounan.Downloader.Worker.Interfaces;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Worker.Services;

internal partial class VideoMergingService : IVideoMergingService
{
    private readonly VideoServiceConfig _videoServiceConfig;

    public VideoMergingService(
        ILogger<VideoMergingService> logger,
        IOptions<VideoServiceConfig> videoServiceConfig)
    {
        Logger = logger;

        ArgumentNullException.ThrowIfNull(videoServiceConfig, nameof(videoServiceConfig));
        _videoServiceConfig = videoServiceConfig.Value;
    }

    private ILogger<VideoMergingService> Logger { get; }

    public async Task DownloadToPipeAsync(Uri playlistUrl, PipeWriter pipeWriter, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();

        var videoParts = await GetVideoPartsAsync(playlistUrl, httpClient, cancellationToken);
        Log.GotVideoParts(Logger, videoParts);

        await ConvertAsync(videoParts, httpClient, pipeWriter, cancellationToken);
        Log.FinishedMerging(Logger);
    }

    private static async Task<IList<Uri>> GetVideoPartsAsync(
        Uri playlist,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(playlist, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var videoParts = content
            .Split('\n')
            .Where(line => line.StartsWith("./", StringComparison.Ordinal))
            .Select(relativeFilePath => new Uri(playlist, relativeFilePath))
            .ToArray();

        return videoParts;
    }

    private Task ConvertAsync(
        IList<Uri> videoParts,
        HttpClient httpClient,
        PipeWriter pipeWriter,
        CancellationToken cancellationToken)
    {
        return SemiConcurrentProcessingHelper.Process(
            videoParts,
            async (partUri, i, total, ct) => await DownloadPartAsync(partUri, i, total, httpClient, ct),
            async (partBytes, i, total, ct) => await PipePartAsync(partBytes, i, total, pipeWriter, ct),
            _videoServiceConfig.ConcurrentDownloads,
            cancellationToken);
    }

    private async Task<byte[]> DownloadPartAsync(
        Uri uri,
        int currentUrlIndex,
        int totalUrls,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetByteArrayAsync(uri, cancellationToken);
        Log.DownloadedPart(Logger, currentUrlIndex + 1, totalUrls);
        return response;
    }

    private async Task PipePartAsync(
        byte[] videoPartBytes,
        int currentPartIndex,
        int totalParts,
        PipeWriter pipeWriter,
        CancellationToken cancellationToken)
    {
        await pipeWriter.WriteAsync(videoPartBytes, cancellationToken);
        Log.ProcessedPart(Logger, currentPartIndex + 1, totalParts);
    }
}