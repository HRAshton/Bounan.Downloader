using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Helpers;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Models;
using Bounan.LoanApi.Interfaces;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace Bounan.Downloader.Worker.Services;

public partial class VideoCopyingService : IVideoCopyingService
{
	private readonly TelegramConfig _telegramConfig;
	private readonly VideoServiceConfig _videoServiceConfig;

	public VideoCopyingService(
		ILogger<VideoCopyingService> logger,
		IOptions<VideoServiceConfig> videoServiceConfig,
		IOptions<TelegramConfig> telegramConfig,
		ILinkValidator linkValidator,
		ILoanApiClient loanApiClient,
		HttpClient httpClient,
		IFfmpegFactory ffmpegFactory,
		ITelegramBotClient telegramClient)
	{
		Logger = logger;
		LinkValidator = linkValidator;
		LoanApiClient = loanApiClient;
		HttpClient = httpClient;
		FfmpegFactory = ffmpegFactory;
		TelegramClient = telegramClient;

		ArgumentNullException.ThrowIfNull(telegramConfig, nameof(telegramConfig));
		ArgumentNullException.ThrowIfNull(videoServiceConfig, nameof(videoServiceConfig));
		_telegramConfig = telegramConfig.Value;
		_videoServiceConfig = videoServiceConfig.Value;
	}

	private ILogger<VideoCopyingService> Logger { get; }

	private HttpClient HttpClient { get; }

	private ILinkValidator LinkValidator { get; }

	private ILoanApiClient LoanApiClient { get; }

	private IFfmpegFactory FfmpegFactory { get; }

	private ITelegramBotClient TelegramClient { get; }

	public async Task ProcessVideo(string message, CancellationToken cancellationToken)
	{
		if (!LinkValidator.IsValidSignedLink(message))
		{
			throw new ArgumentException("Invalid signed url");
		}

		var signedUri = new Uri(message);
		Log.ProcessingVideo(Logger, signedUri);

		using var ffmpegService = FfmpegFactory.CreateFfmpegService(cancellationToken);
		var (videoInfo, thumbnail) = await DownloadAndMergeVideoAsync(signedUri, ffmpegService, cancellationToken);
		Log.GotVideoInfo(Logger, videoInfo);

		var fileId = await UploadVideoAsync(videoInfo, thumbnail, message, cancellationToken);
		Log.VideoUploaded(Logger, fileId);
	}

	private async Task<(VideoInfo VideoInfo, Uri Thumbnail)> DownloadAndMergeVideoAsync(
		Uri signedUri,
		IFfmpegService ffmpegService,
		CancellationToken cancellationToken)
	{
		var (playlists, thumb) = await LoanApiClient.GetPlaylistsAndThumbnailUrlsAsync(signedUri, cancellationToken);
		Log.GotPlaylistsAndThumbnail(Logger, playlists, thumb);

		var bestQualityPlaylist = playlists.Last().Value;
		Log.ProcessingPlaylist(Logger, bestQualityPlaylist);

		var videoParts = await GetVideoPartsAsync(bestQualityPlaylist, cancellationToken);
		Log.GotVideoParts(Logger, videoParts);

		await SemiConcurrentProcessingHelper.Process(
			videoParts,
			async (uri, i, total, ct) =>
			{
				var response = await HttpClient.GetByteArrayAsync(uri, ct);
				Log.DownloadedPart(Logger, i + 1, total);
				return response;
			},
			async (bytes, i, total, ct) =>
			{
				await ffmpegService.PipeWriter.WriteAsync(bytes, ct);
				Log.ProcessedPart(Logger, i + 1, total);
			},
			_videoServiceConfig.ConcurrentDownloads,
			cancellationToken);

		await ffmpegService.PipeWriter.CompleteAsync();
		var videoInfo = await ffmpegService.GetResultAsync(cancellationToken);

		return (videoInfo, thumb);
	}

	private async Task<string> UploadVideoAsync(
		VideoInfo videoInfo,
		Uri thumbnailUri,
		string originalSignedLink,
		CancellationToken cancellationToken)
	{
		await using var fileStream = File.OpenRead(videoInfo.FilePath);
		await using var thumbStream = await HttpClient.GetStreamAsync(thumbnailUri, cancellationToken);

		var message = await TelegramClient.SendVideoAsync(
			_telegramConfig.DestinationChatId,
			new InputFileStream(fileStream),
			caption: originalSignedLink,
			width: videoInfo.Width,
			height: videoInfo.Height,
			duration: videoInfo.DurationSec,
			thumbnail: new InputFileStream(thumbStream),
			supportsStreaming: true,
			cancellationToken: cancellationToken);
		Log.VideoUploaded(Logger);

		var fileId = message.Video!.FileId;

		return fileId;
	}

	private async Task<IList<Uri>> GetVideoPartsAsync(Uri playlist, CancellationToken cancellationToken)
	{
		var response = await HttpClient.GetAsync(playlist, cancellationToken);
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		var videoParts = content
			.Split('\n')
			.Where(line => line.StartsWith("./", StringComparison.Ordinal))
			.Select(relativeFilePath => new Uri(playlist, relativeFilePath))
			.ToArray();

		return videoParts;
	}
}