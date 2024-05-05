using Bounan.Downloader.Hls2TlgrUploader.Configuration;
using Bounan.Downloader.Hls2TlgrUploader.Interfaces;
using Bounan.Downloader.Hls2TlgrUploader.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace Bounan.Downloader.Hls2TlgrUploader.Services;

internal sealed partial class VideoUploadingService : IVideoUploadingService
{
    private readonly TelegramConfig _telegramConfig;

    public VideoUploadingService(
        ILogger<VideoUploadingService> logger,
        IOptions<TelegramConfig> telegramConfig,
        IFfmpegFactory ffmpegFactory,
        ITelegramBotClient telegramClient,
        IVideoMergingService videoMergingService)
    {
        Logger = logger;
        FfmpegFactory = ffmpegFactory;
        TelegramClient = telegramClient;
        VideoMergingService = videoMergingService;

        ArgumentNullException.ThrowIfNull(telegramConfig, nameof(telegramConfig));
        _telegramConfig = telegramConfig.Value;
    }

    private ILogger<VideoUploadingService> Logger { get; }

    private IFfmpegFactory FfmpegFactory { get; }

    private IVideoMergingService VideoMergingService { get; }

    private ITelegramBotClient TelegramClient { get; }

    public async Task<int> CopyToTelegramAsync(
        IList<Uri> hlsParts,
        Task<Stream> jpegThumbnailStreamTask,
        string caption,
        CancellationToken cancellationToken)
    {
        Log.StartedCopying(Logger, hlsParts.Count);

        using var ffmpegService = FfmpegFactory.CreateFfmpegService(cancellationToken);
        var videoInfo = await DownloadAndMergeVideoAsync(hlsParts, ffmpegService, cancellationToken);
        Log.GotVideoInfo(Logger, videoInfo);

        await using var thumbnailStream = await jpegThumbnailStreamTask;
        Log.ThumbnailFetched(Logger);

        var messageId = await UploadVideoAsync(videoInfo, thumbnailStream, caption, cancellationToken);
        Log.VideoUploaded(Logger, messageId);

        return messageId;
    }

    private async Task<VideoInfo> DownloadAndMergeVideoAsync(
        IList<Uri> hlsParts,
        IFfmpegService ffmpegService,
        CancellationToken cancellationToken)
    {
        await VideoMergingService.DownloadToPipeAsync(hlsParts, ffmpegService.PipeWriter, cancellationToken);

        await ffmpegService.PipeWriter.CompleteAsync();
        var videoInfo = await ffmpegService.GetResultAsync(cancellationToken);

        return videoInfo;
    }

    private async Task<int> UploadVideoAsync(
        VideoInfo videoInfo,
        Stream thumbnailStream,
        string caption,
        CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(videoInfo.FilePath);

        var message = await TelegramClient.SendVideoAsync(
            _telegramConfig.DestinationChatId,
            new InputFileStream(fileStream),
            caption: caption,
            width: videoInfo.Width,
            height: videoInfo.Height,
            duration: videoInfo.DurationSec,
            thumbnail: new InputFileStream(thumbnailStream),
            supportsStreaming: true,
            cancellationToken: cancellationToken);
        Log.VideoUploaded(Logger);

        return message.MessageId;
    }
}