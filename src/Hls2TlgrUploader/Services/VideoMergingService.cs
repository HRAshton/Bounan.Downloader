using System.IO.Pipelines;
using Bounan.Downloader.Hls2TlgrUploader.Configuration;
using Bounan.Downloader.Hls2TlgrUploader.Helpers;
using Bounan.Downloader.Hls2TlgrUploader.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Hls2TlgrUploader.Services;

internal sealed partial class VideoMergingService(
    ILogger<VideoMergingService> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<ProcessingConfig> videoServiceConfig)
    : IVideoMergingService
{
    private readonly ProcessingConfig _processingConfig = videoServiceConfig.Value;

    private ILogger<VideoMergingService> Logger => logger;

    private IHttpClientFactory HttpClientFactory => httpClientFactory;

    public async Task DownloadToPipeAsync(
        IList<Uri> hlsParts,
        PipeWriter pipeWriter,
        CancellationToken cancellationToken)
    {
        Log.StartedMerging(Logger, hlsParts.Count);

        using var httpClient = HttpClientFactory.CreateClient();

        await SemiConcurrentProcessingHelper.Process(
            hlsParts,
            async (partUri, i, total, ct) => await DownloadPartAsync(partUri, i, total, httpClient, ct),
            async (partBytes, i, total, ct) => await PipePartAsync(partBytes, i, total, pipeWriter, ct),
            _processingConfig.ConcurrentDownloads,
            cancellationToken);

        Log.FinishedMerging(Logger);
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