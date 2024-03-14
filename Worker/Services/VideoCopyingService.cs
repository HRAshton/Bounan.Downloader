using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Extensions;
using Bounan.Downloader.Worker.Helpers;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Models;
using Bounan.LoanApi.Interfaces;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace Bounan.Downloader.Worker.Services;

public class VideoCopyingService : IVideoCopyingService
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

		_telegramConfig = telegramConfig.Value;
		_videoServiceConfig = videoServiceConfig.Value;
	}

	private ILogger<VideoCopyingService> Logger { get; }

	private ILinkValidator LinkValidator { get; }

	private ILoanApiClient LoanApiClient { get; }

	private HttpClient HttpClient { get; }

	private IFfmpegFactory FfmpegFactory { get; }

	private ITelegramBotClient TelegramClient { get; }

	public async Task ProcessVideo(string message, CancellationToken cancellationToken)
	{
		using var _ = Logger.BeginScope("msgId={Hash}", message.CalculateHash());

		if (!LinkValidator.IsValidSignedLink(message))
		{
			throw new ArgumentException("Invalid signed url");
		}

		var signedUri = new Uri(message);
		Logger.LogDebug("Processing video: {SignedUrl}", signedUri);

		using var ffmpegService = FfmpegFactory.CreateFfmpegService(cancellationToken);
		var (videoInfo, thumbnail) = await DownloadAndMergeVideoAsync(signedUri, ffmpegService, cancellationToken);
		Logger.LogDebug("Got video info: {VideoInfo}", videoInfo);

		var fileId = await UploadVideoAsync(videoInfo, thumbnail, message, cancellationToken);
		Logger.LogInformation("Video uploaded with file id: {FileId}", fileId);
	}

	private async Task<(VideoInfo VideoInfo, Uri Thumbnail)> DownloadAndMergeVideoAsync(
		Uri signedUri,
		IFfmpegService ffmpegService,
		CancellationToken cancellationToken)
	{
		var (playlists, thumb) = await LoanApiClient.GetPlaylistsAndThumbnailUrlsAsync(signedUri, cancellationToken);
		Logger.LogDebug("Got playlists and thumb: {Playlists} {Thumbnail}", string.Join(',', playlists), thumb);

		var bestQualityPlaylist = playlists.Last().Value;
		Logger.LogDebug("Processing playlist: {Playlist}", bestQualityPlaylist);

		var videoParts = await GetVideoPartsAsync(bestQualityPlaylist, cancellationToken);
		Logger.LogDebug("Got video parts: {VideoParts}", string.Join(',', videoParts));

		await SemiConcurrentProcessingHelper.Process(
			videoParts,
			async (uri, i, total, ct) =>
			{
				var response = await HttpClient.GetByteArrayAsync(uri, ct);
				Logger.LogTrace("Downloaded part {Index}/{Total}", i + 1, total);
				return response;
			},
			async (bytes, i, total, ct) =>
			{
				await ffmpegService.PipeWriter.WriteAsync(bytes, ct);
				Logger.LogTrace("Processed part {Index}/{Total}", i + 1, total);
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

		var message = await TelegramClient.SendVideoAsync(
			_telegramConfig.DestinationChatId,
			new InputFileStream(fileStream),
			caption: originalSignedLink,
			width: videoInfo.Width,
			height: videoInfo.Height,
			duration: videoInfo.DurationSec,
			thumbnail: new InputFileUrl(thumbnailUri),
			supportsStreaming: true,
			cancellationToken: cancellationToken);
		Logger.LogDebug("Video uploaded");

		var fileId = message.Video!.FileId;

		return fileId;
	}

	private async Task<IList<Uri>> GetVideoPartsAsync(Uri playlist, CancellationToken cancellationToken)
	{
		var response = await HttpClient.GetAsync(playlist, cancellationToken);
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		var videoParts = content
			.Split('\n')
			.Where(line => line.StartsWith("./"))
			.Select(relativeFilePath => new Uri(playlist, relativeFilePath))
			.ToArray();

		return videoParts;
	}
}