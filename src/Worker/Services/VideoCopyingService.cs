using System.Diagnostics.CodeAnalysis;
using System.Text;
using Bounan.Common;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Helpers;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.LoanApi.Interfaces;
using Hls2TlgrUploader.Interfaces;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Bounan.Downloader.Worker.Services;

internal partial class VideoCopyingService(
    ILogger<VideoCopyingService> logger,
    IOptions<ProcessingConfig> processingConfig,
    IHttpClientFactory httpClientFactory,
    ILoanApiComClient loanApiComClient,
    ILoanApiInfoClient loanApiInfoClient,
    IAniManClient aniManClient,
    IThumbnailService thumbnailService,
    IVideoUploadingService videoUploadingService)
    : IVideoCopyingService
{
    private readonly ProcessingConfig _processingConfig = processingConfig.Value;

    private ILogger<VideoCopyingService> Logger => logger;

    private IHttpClientFactory HttpClientFactory => httpClientFactory;

    private IAniManClient AniManClient => aniManClient;

    private ILoanApiComClient LoanApiComClient => loanApiComClient;

    private ILoanApiInfoClient LoanApiInfoClient => loanApiInfoClient;

    private IVideoUploadingService VideoUploadingService => videoUploadingService;

    private IThumbnailService ThumbnailService => thumbnailService;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public async Task ProcessVideo(IVideoKey videoKey, CancellationToken cancellationToken)
    {
        Log.ReceivedVideoKey(Logger, videoKey);
        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        innerCts.CancelAfter(_processingConfig.TimeoutSeconds * 1000);

        ArgumentNullException.ThrowIfNull(videoKey);
        try
        {
            await Retry.DoAsync(
                async ct => await ProcessVideoInternalAsync(videoKey, ct),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Log.ErrorProcessingVideo(Logger, e);
            await SendResult(videoKey, null, innerCts.Token);
        }
    }

    private async Task ProcessVideoInternalAsync(IVideoKey videoKey, CancellationToken cancellationToken)
    {
        var signedUri = await LoanApiComClient.GetRequiredSignedLinkAsync(videoKey, cancellationToken);
        Log.ProcessingVideo(Logger, signedUri);

        var (playlistUri, origThumbnail) = await GetPlaylistAndThumbnailAsync(signedUri, cancellationToken);
        Log.GotVideoInfo(Logger, playlistUri, origThumbnail);

        var videoParts = await GetVideoPartsAsync(playlistUri, cancellationToken);

        var thumbnailStreamTask = ThumbnailService.GetThumbnailJpegStreamAsync(
            origThumbnail,
            videoKey,
            cancellationToken);

        var videoMetadata = new VideoMetadata(videoKey, signedUri.ToString());

        var message = await VideoUploadingService.CopyToTelegramAsync(
            videoParts,
            thumbnailStreamTask,
            EncodeMetadata(videoMetadata),
            cancellationToken);
        Log.VideoUploaded(Logger, message.MessageId);

        await SendResult(videoKey, message.MessageId, cancellationToken);
    }

    private async Task<(Uri Playlist, Uri Thumbnail)> GetPlaylistAndThumbnailAsync(
        Uri signedUri,
        CancellationToken cancellationToken)
    {
        var (playlists, thumb) =
            await LoanApiInfoClient.GetPlaylistsAndThumbnailUrlsAsync(signedUri, cancellationToken);
        Log.GotPlaylistsAndThumbnail(Logger, playlists, thumb);

        var sortedPlaylists = playlists
            .OrderBy(pair => pair.Key.Length)
            .ThenBy(pair => pair.Key);
        var bestQualityPlaylist = _processingConfig.UseLowestQuality
            ? sortedPlaylists.First().Value
            : sortedPlaylists.Last().Value;
        Log.ProcessingPlaylist(Logger, bestQualityPlaylist);

        return (bestQualityPlaylist, thumb);
    }

    private async Task<IList<Uri>> GetVideoPartsAsync(Uri playlist, CancellationToken cancellationToken)
    {
        using var httpClient = HttpClientFactory.CreateClient();

        var response = await httpClient.GetAsync(playlist, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var videoParts = content
            .Split('\n')
            .Where(line => line.StartsWith("./", StringComparison.Ordinal))
            .Select(relativeFilePath => new Uri(playlist, relativeFilePath))
            .ToArray();

        return videoParts;
    }

    private async Task SendResult(IVideoKey videoKey, int? messageId, CancellationToken cancellationToken)
    {
        var key = new VideoKey(videoKey.MyAnimeListId, videoKey.Dub, videoKey.Episode);
        var dwnResult = new DownloaderResultRequest(key, messageId);
        await AniManClient.SendResult(dwnResult, cancellationToken);
        Log.ResultSent(Logger, dwnResult);
    }

    private static string EncodeMetadata(VideoMetadata metadata)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata)));
    }

    private record VideoMetadata([UsedImplicitly] IVideoKey VideoKey, [UsedImplicitly] string SignedLink);
}