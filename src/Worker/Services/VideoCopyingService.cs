using System.Diagnostics.CodeAnalysis;
using System.Text;
using Bounan.Common.Models;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Models;
using Bounan.LoanApi.Interfaces;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace Bounan.Downloader.Worker.Services;

internal partial class VideoCopyingService : IVideoCopyingService
{
    private readonly TelegramConfig _telegramConfig;
    private readonly VideoServiceConfig _videoServiceConfig;

    public VideoCopyingService(
        ILogger<VideoCopyingService> logger,
        IOptions<VideoServiceConfig> videoServiceConfig,
        IOptions<TelegramConfig> telegramConfig,
        ILoanApiComClient loanApiComClient,
        ILoanApiInfoClient loanApiInfoClient,
        IFfmpegFactory ffmpegFactory,
        ITelegramBotClient telegramClient,
        IAniManClient aniManClient,
        IVideoMergingService videoMergingService,
        IThumbnailService thumbnailService)
    {
        Logger = logger;
        LoanApiComClient = loanApiComClient;
        LoanApiInfoClient = loanApiInfoClient;
        FfmpegFactory = ffmpegFactory;
        TelegramClient = telegramClient;
        AniManClient = aniManClient;
        VideoMergingService = videoMergingService;
        ThumbnailService = thumbnailService;

        ArgumentNullException.ThrowIfNull(telegramConfig, nameof(telegramConfig));
        ArgumentNullException.ThrowIfNull(videoServiceConfig, nameof(videoServiceConfig));
        _telegramConfig = telegramConfig.Value;
        _videoServiceConfig = videoServiceConfig.Value;
    }

    private ILogger<VideoCopyingService> Logger { get; }

    private IAniManClient AniManClient { get; }

    private ILoanApiComClient LoanApiComClient { get; }

    private ILoanApiInfoClient LoanApiInfoClient { get; }

    private IFfmpegFactory FfmpegFactory { get; }

    private IVideoMergingService VideoMergingService { get; }

    private IThumbnailService ThumbnailService { get; }

    private ITelegramBotClient TelegramClient { get; }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public async Task ProcessVideo(IVideoKey videoKey, CancellationToken cancellationToken)
    {
        Log.ReceivedVideoKey(Logger, videoKey);
        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        innerCts.CancelAfter(_videoServiceConfig.TimeoutSeconds * 1000);

        ArgumentNullException.ThrowIfNull(videoKey);
        try
        {
            var signedUri = await LoanApiComClient.GetRequiredSignedLinkAsync(videoKey, innerCts.Token);
            Log.ProcessingVideo(Logger, signedUri);

            using var ffmpegService = FfmpegFactory.CreateFfmpegService(innerCts.Token);
            var (videoInfo, origThumbnail) = await DownloadAndMergeVideoAsync(signedUri, ffmpegService, innerCts.Token);
            Log.GotVideoInfo(Logger, videoInfo);

            var videoMetadata = new VideoMetadata(videoKey, signedUri.ToString());

            await using var thumbnailStream = await ThumbnailService.GetThumbnailPngStreamAsync(
                origThumbnail,
                videoKey,
                innerCts.Token);
            
            var fileId = await UploadVideoAsync(videoInfo, thumbnailStream, videoMetadata, innerCts.Token);
            Log.VideoUploaded(Logger, fileId);

            await SendResult(videoKey, fileId, innerCts.Token);
        }
        catch (Exception e)
        {
            Log.ErrorProcessingVideo(Logger, e);
            await SendResult(videoKey, null, innerCts.Token);
        }
    }

    private async Task<(VideoInfo VideoInfo, Uri Thumbnail)> DownloadAndMergeVideoAsync(
        Uri signedUri,
        IFfmpegService ffmpegService,
        CancellationToken cancellationToken)
    {
        var (playlists, thumb) =
            await LoanApiInfoClient.GetPlaylistsAndThumbnailUrlsAsync(signedUri, cancellationToken);
        Log.GotPlaylistsAndThumbnail(Logger, playlists, thumb);

        var sortedPlaylists = playlists
            .OrderBy(pair => pair.Key.Length)
            .ThenBy(pair => pair.Key);
        var bestQualityPlaylist = _videoServiceConfig.UseLowestQuality
            ? sortedPlaylists.First().Value
            : sortedPlaylists.Last().Value;
        Log.ProcessingPlaylist(Logger, bestQualityPlaylist);

        await VideoMergingService.DownloadToPipeAsync(bestQualityPlaylist, ffmpegService.PipeWriter, cancellationToken);

        await ffmpegService.PipeWriter.CompleteAsync();
        var videoInfo = await ffmpegService.GetResultAsync(cancellationToken);

        return (videoInfo, thumb);
    }

    private async Task<string> UploadVideoAsync(
        VideoInfo videoInfo,
        Stream thumbnailStream,
        VideoMetadata videoMetadata,
        CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(videoInfo.FilePath);

        var message = await TelegramClient.SendVideoAsync(
            _telegramConfig.DestinationChatId,
            new InputFileStream(fileStream),
            caption: EncodeMetadata(videoMetadata),
            width: videoInfo.Width,
            height: videoInfo.Height,
            duration: videoInfo.DurationSec,
            thumbnail: new InputFileStream(thumbnailStream),
            supportsStreaming: true,
            cancellationToken: cancellationToken);
        Log.VideoUploaded(Logger);

        var fileId = message.Video!.FileId;
        return fileId;
    }

    private async Task SendResult(IVideoKey videoKey, string? fileId, CancellationToken cancellationToken)
    {
        var dwnResult = new DwnResultNotification(videoKey.MyAnimeListId, videoKey.Dub, videoKey.Episode, fileId);
        await AniManClient.SendResult(dwnResult, cancellationToken);
        Log.ResultSent(Logger, dwnResult);
    }

    private static string EncodeMetadata(VideoMetadata metadata)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata)));
    }

    private record VideoMetadata([UsedImplicitly] IVideoKey VideoKey, [UsedImplicitly] string SignedLink);
}