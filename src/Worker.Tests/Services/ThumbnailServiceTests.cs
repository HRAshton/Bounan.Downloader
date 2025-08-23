using System.Net;
using Bounan.Common;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Helpers;
using Bounan.Downloader.Worker.Services;
using Bounan.LoanApi.Interfaces;
using Bounan.LoanApi.RefitClients.LoanApiCom.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bounan.Downloader.Worker.Tests.Services;

public class ThumbnailServiceTests
{
    [Test]
    public void GetThumbnailPngStreamAsync_OriginalThumbnailUrlIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var thumbnailService = new ThumbnailService(
            NullLogger<ThumbnailService>.Instance,
            Options.Create(new ThumbnailConfig { BotId = "@" }),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILoanApiComClient>());

        // Act
        async Task Act() =>
            await thumbnailService.GetThumbnailJpegStreamAsync(null!, Mock.Of<IVideoKey>(), CancellationToken.None);

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(Act, "Value cannot be null. (Parameter 'originalThumbnailUrl')");
    }

    [Test]
    public void GetThumbnailPngStreamAsync_VideoKeyIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var thumbnailService = new ThumbnailService(
            NullLogger<ThumbnailService>.Instance,
            Options.Create(new ThumbnailConfig { BotId = "@" }),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILoanApiComClient>());

        // Act
        async Task Act() =>
            await thumbnailService.GetThumbnailJpegStreamAsync(
                new Uri("https://example.com"),
                null!,
                CancellationToken.None);

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(Act, "Value cannot be null. (Parameter 'videoKey')");
    }

    [TestCase(
        1,
        "Непризнанный школой владыка демонов! Сильнейший владыка демонов в истории поступает в академию, переродившись своим потомком",
        "Lover's Choice",
        100000)]
    [TestCase(2, "The Eminence in Shadow", "Crunopyshall.Subtitles", 12)]
    public async Task GetThumbnailPngStreamAsync_ApplyWatermarkIsTrue_ReturnsImageStream(
        int testId,
        string animeName,
        string dub,
        int episode)
    {
        // Arrange
        byte[] baseImageBytes = await File.ReadAllBytesAsync("Assets/thumbnail.jpg");

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK, Content = new ByteArrayContent(baseImageBytes),
                });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(httpMessageHandlerMock.Object));

        var loanApiComClientMock = new Mock<ILoanApiComClient>();
        loanApiComClientMock
            .Setup(x => x.SearchAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SearchResult(
                [
                    new SearchResultItem(animeName, string.Empty),
                ]));

        var thumbnailService = new ThumbnailService(
            NullLogger<ThumbnailService>.Instance,
            Options.Create(new ThumbnailConfig { BotId = "@aaaaaa_aaaaa_bot" }),
            httpClientFactory.Object,
            loanApiComClientMock.Object);

        // Act
        await using var stream = await thumbnailService.GetThumbnailJpegStreamAsync(
            new Uri("https://example.com"),
            new VideoKey(0, dub, episode),
            CancellationToken.None);

        // Assert
        using var image = Image.Load<Rgba32>(stream);
        await image.SaveAsPngAsync($"../../../Out/output{testId}.jpg");
        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(320));
            Assert.That(image.Height, Is.EqualTo(180));
        });

        Image<Rgba32> load = Image.Load<Rgba32>($"Assets/sample{testId}.jpg");
        double mse = ComputeMse(image, load);
        Assert.That(mse, Is.LessThan(1), "MSE is too high");
    }

    private static double ComputeMse(Image<Rgba32> a, Image<Rgba32> b)
    {
        Guard.Ensure(a.Size == b.Size, "Images must be the same size");

        // sum squared error over all channels
        double totalError = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                var p = a[x, y];
                var q = b[x, y];
                totalError += Square(p.R - q.R);
                totalError += Square(p.G - q.G);
                totalError += Square(p.B - q.B);
            }
        }

        // average per-channel
        double msePerChannel = totalError / (a.Width * a.Height * 3);
        return msePerChannel;
    }

    private static double Square(double v) => v * v;
}