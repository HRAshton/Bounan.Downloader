using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Bounan.Common.Models;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Extensions;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.LoanApi.Interfaces;
using Microsoft.Extensions.Options;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bounan.Downloader.Worker.Services;

internal partial class ThumbnailService : IThumbnailService
{
    private readonly ThumbnailConfig _thumbnailConfig;

    public ThumbnailService(
        ILogger<ThumbnailService> logger,
        IOptions<ThumbnailConfig> thumbnailConfig,
        IHttpClientFactory httpClientFactory,
        ILoanApiComClient loanApiComClient)
    {
        Logger = logger;
        HttpClientFactory = httpClientFactory;
        LoanApiComClient = loanApiComClient;

        _thumbnailConfig = thumbnailConfig.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_thumbnailConfig.BotId);
    }

    private ILogger<ThumbnailService> Logger { get; }

    private IHttpClientFactory HttpClientFactory { get; }

    private ILoanApiComClient LoanApiComClient { get; }

    [SuppressMessage("ReSharper", "AccessToDisposedClosure", Justification = "Reviewed")]
    public async Task<Stream> GetThumbnailPngStreamAsync(
        Uri originalThumbnailUrl,
        IVideoKey videoKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalThumbnailUrl);
        ArgumentNullException.ThrowIfNull(videoKey);

        using var image = await GetOriginalImageAsync(originalThumbnailUrl, cancellationToken);
        Log.GotOriginalImage(Logger, image.Width, image.Height);

        if (!_thumbnailConfig.ApplyWatermark)
        {
            var imageStream = new MemoryStream();
            await image.SaveAsPngAsync(imageStream, cancellationToken);
            imageStream.Position = 0;
            Log.OriginalImageUsedAsThumbnail(Logger);

            return imageStream;
        }

        var animeName = await GetAnimeNameAsync(videoKey.MyAnimeListId, cancellationToken);
        Log.GotAnimeName(Logger, videoKey, animeName);

        using var thumbnail = CreateWatermark(animeName, videoKey.Dub, videoKey.Episode, _thumbnailConfig.BotId);
        Log.CreatedWatermark(Logger, thumbnail.Width, thumbnail.Height);

        thumbnail.Mutate(ctx => ctx.Resize(image.Width, image.Height));
        image.Mutate(ctx => ctx.DrawImage(thumbnail, 1));
        Log.DrawnWatermark(Logger);

        var thumbnailStream = new MemoryStream();
        await image.SaveAsJpegAsync(thumbnailStream, cancellationToken);
        thumbnailStream.Position = 0;
        Log.SavedThumbnail(Logger);

        return thumbnailStream;
    }

    private async Task<string> GetAnimeNameAsync(int malId, CancellationToken cancellationToken)
    {
        var searchResult = await LoanApiComClient.SearchAsync(malId, cancellationToken);
        var differentResults = searchResult.Results
            .Select(result => result.Title)
            .Distinct()
            .ToArray();
        if (differentResults.Length != 1)
        {
            Log.DifferentAnimeNames(Logger, differentResults);
        }

        return searchResult.Results.First().Title;
    }

    private async Task<Image> GetOriginalImageAsync(
        Uri originalThumbnailUrl,
        CancellationToken cancellationToken)
    {
        using var httpClient = HttpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(originalThumbnailUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var originalImageStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var image = await Image.LoadAsync(originalImageStream, cancellationToken);

        return image;
    }

    private static Image<Rgba32> CreateWatermark(string animeName, string dub, int episode, string botName)
    {
        var watermark = new Image<Rgba32>(Geometry.TmbWidth, Geometry.TmbHeight);
        watermark.Mutate(ctx =>
        {
            DrawBotName(botName, ctx);
            DrawAnimeName(animeName, ctx);
            if (episode > 0) DrawEpisode(episode, ctx);
            if (!string.IsNullOrWhiteSpace(dub)) DrawDub(dub, ctx);
        });

        return watermark;
    }

    private static void DrawBotName(string botName, IImageProcessingContext ctx)
    {
        ctx
            .FillPolygon(Rgba32.ParseHex("#FFFFFFE6"), Geometry.BotName.Polygon)
            .ApplyScalingWaterMarkSimple(
                SystemFonts.CreateFont("Roboto Light", 90),
                botName,
                Color.Black,
                Geometry.BotName.TextRectangle);
    }

    private static void DrawAnimeName(string animeName, IImageProcessingContext ctx)
    {
        ctx
            .FillPolygon(Rgba32.ParseHex("#FF6666B3"), Geometry.AnimeName.SmallLeft.Polygon)
            .FillPolygon(Rgba32.ParseHex("#FF6666E6"), Geometry.AnimeName.MediumLeft.Polygon)
            .FillPolygon(Rgba32.ParseHex("#FF6666F2"), Geometry.AnimeName.Large.Polygon)
            .FillPolygon(Rgba32.ParseHex("#FF6666E6"), Geometry.AnimeName.MediumRight.Polygon)
            .FillPolygon(Rgba32.ParseHex("#FF6666B3"), Geometry.AnimeName.SmallRight.Polygon)
            .ApplyScalingWaterMarkWordWrap(
                SystemFonts.CreateFont("Roboto Medium", 150),
                animeName.ToUpper(CultureInfo.InvariantCulture),
                Color.White,
                Geometry.AnimeName.Large.TextRectangle);
    }

    private static void DrawEpisode(int episode, IImageProcessingContext ctx)
    {
        ctx
            .FillPolygon(Rgba32.ParseHex("#FFFFFFE6"), Geometry.Episode.Polygon)
            .ApplyScalingWaterMarkSimple(
                SystemFonts.CreateFont("Roboto Medium", 90),
                $"СЕРИЯ {episode}",
                Color.Black,
                Geometry.Episode.TextRectangle);
    }

    private static void DrawDub(string dub, IImageProcessingContext ctx)
    {
        ctx
            .FillPolygon(Rgba32.ParseHex("#FFFFFFE6"), Geometry.Dub.Polygon)
            .ApplyScalingWaterMarkWordWrap(
                SystemFonts.CreateFont("Roboto Medium", 90),
                dub,
                Color.Black,
                Geometry.Dub.TextRectangle);
    }
}