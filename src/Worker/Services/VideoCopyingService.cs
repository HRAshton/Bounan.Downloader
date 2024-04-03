using System.Diagnostics.CodeAnalysis;
using System.Text;
using Bounan.Common.Models;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Helpers;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Models;
using Bounan.LoanApi.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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
		ILoanApiComClient loanApiComClient,
		ILoanApiInfoClient loanApiInfoClient,
		HttpClient httpClient,
		IFfmpegFactory ffmpegFactory,
		ITelegramBotClient telegramClient,
		IAniManClient aniManClient)
	{
		Logger = logger;
		LinkValidator = linkValidator;
		LoanApiComClient = loanApiComClient;
		LoanApiInfoClient = loanApiInfoClient;
		HttpClient = httpClient;
		FfmpegFactory = ffmpegFactory;
		TelegramClient = telegramClient;
		AniManClient = aniManClient;

		ArgumentNullException.ThrowIfNull(telegramConfig, nameof(telegramConfig));
		ArgumentNullException.ThrowIfNull(videoServiceConfig, nameof(videoServiceConfig));
		_telegramConfig = telegramConfig.Value;
		_videoServiceConfig = videoServiceConfig.Value;
	}

	private ILogger<VideoCopyingService> Logger { get; }

	private IAniManClient AniManClient { get; }

	private HttpClient HttpClient { get; }

	private ILinkValidator LinkValidator { get; }

	private ILoanApiComClient LoanApiComClient { get; }

	private ILoanApiInfoClient LoanApiInfoClient { get; }

	private IFfmpegFactory FfmpegFactory { get; }

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
			var signedUri = await FindSignedLink(videoKey);
			Log.ProcessingVideo(Logger, signedUri);

			using var ffmpegService = FfmpegFactory.CreateFfmpegService(innerCts.Token);
			var (videoInfo, thumbnail) = await DownloadAndMergeVideoAsync(signedUri, ffmpegService, innerCts.Token);
			Log.GotVideoInfo(Logger, videoInfo);

			var videoMetadata = new VideoMetadata(videoKey, signedUri.ToString());

			var fileId = await UploadVideoAsync(videoInfo, thumbnail, videoMetadata, innerCts.Token);
			Log.VideoUploaded(Logger, fileId);

			await SendResult(videoKey, fileId, cancellationToken);
		}
		catch (Exception e)
		{
			Log.ErrorProcessingVideo(Logger, e);
			await SendResult(videoKey, null, cancellationToken);
		}
	}

	private async Task<Uri> FindSignedLink(IVideoKey videoKey)
	{
		var searchResult = await LoanApiComClient.SearchAsync(videoKey.MyAnimeListId, CancellationToken.None);
		var bestQualityVideo = searchResult
			.LastOrDefault(res => res.Dub == videoKey.Dub && res.Episode == videoKey.Episode);

		var signedLink = bestQualityVideo?.SignedLink ?? string.Empty;
		if (!LinkValidator.IsValidSignedLink(signedLink))
		{
			throw new ArgumentException("Invalid signed url");
		}

		return new Uri(signedLink);
	}

	private async Task<(VideoInfo VideoInfo, Uri Thumbnail)> DownloadAndMergeVideoAsync(
		Uri signedUri,
		IFfmpegService ffmpegService,
		CancellationToken cancellationToken)
	{
		var (playlists, thumb) =
			await LoanApiInfoClient.GetPlaylistsAndThumbnailUrlsAsync(signedUri, cancellationToken);
		Log.GotPlaylistsAndThumbnail(Logger, playlists, thumb);

		var bestQualityPlaylist = playlists
			.OrderBy(pair => pair.Key.Length)
			.ThenBy(pair => pair.Key)
			// .Reverse() // Gets the lowest quality for debugging purposes
			.Last()
			.Value;
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
		VideoMetadata videoMetadata,
		CancellationToken cancellationToken)
	{
		await using var fileStream = File.OpenRead(videoInfo.FilePath);
		await using var thumbStream = await HttpClient.GetStreamAsync(thumbnailUri, cancellationToken);

		var message = await TelegramClient.SendVideoAsync(
			_telegramConfig.DestinationChatId,
			new InputFileStream(fileStream),
			caption: EncodeMetadata(videoMetadata),
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
}