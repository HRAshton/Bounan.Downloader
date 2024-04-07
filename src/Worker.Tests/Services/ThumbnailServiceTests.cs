using System.Net;
using Bounan.Common.Models;
using Bounan.Downloader.Worker.Configuration;
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
            await thumbnailService.GetThumbnailPngStreamAsync(null!, Mock.Of<IVideoKey>(), CancellationToken.None);

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
            await thumbnailService.GetThumbnailPngStreamAsync(
                new Uri("https://example.com"),
                null!,
                CancellationToken.None);

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(Act, "Value cannot be null. (Parameter 'videoKey')");
    }

    [Test]
    public async Task GetThumbnailPngStreamAsync_ApplyWatermarkIsFalse_ReturnsImageStream()
    {
        // Arrange
        using var testWatermark = new Image<Rgba32>(1, 2);
        using var memoryStream = new MemoryStream();
        await testWatermark.SaveAsPngAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(bytes),
            });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(httpMessageHandlerMock.Object));

        var thumbnailService = new ThumbnailService(
            NullLogger<ThumbnailService>.Instance,
            Options.Create(new ThumbnailConfig
            {
                ApplyWatermark = false,
                BotId = "@",
            }),
            httpClientFactory.Object,
            Mock.Of<ILoanApiComClient>());

        // Act
        await using var stream = await thumbnailService.GetThumbnailPngStreamAsync(
            new Uri("https://example.com"),
            Mock.Of<IVideoKey>(),
            CancellationToken.None);

        // Assert
        using var image = Image.Load<Rgba32>(stream);
        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(1));
            Assert.That(image.Height, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetThumbnailPngStreamAsync_ApplyWatermarkIsTrue_ReturnsImageStream()
    {
        // Arrange
        var bytes = await File.ReadAllBytesAsync("Assets/thumbnail1.jpg");

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(bytes),
            });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(httpMessageHandlerMock.Object));

        var loanApiComClientMock = new Mock<ILoanApiComClient>();
        loanApiComClientMock
            .Setup(x => x.SearchAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult(new[]
            {
                new SearchResultItem(
                    "Непризнанный школой владыка демонов! " +
                    "Сильнейший владыка демонов в истории поступает в академию, переродившись своим потомком",
                    //  "Бедствие ли это?",
                    string.Empty),
            }));

        var thumbnailService = new ThumbnailService(
            NullLogger<ThumbnailService>.Instance,
            Options.Create(new ThumbnailConfig
            {
                ApplyWatermark = true,
                BotId = "@uni_ru_anime_bot",
            }),
            httpClientFactory.Object,
            loanApiComClientMock.Object);

        // Act
        await using var stream = await thumbnailService.GetThumbnailPngStreamAsync(
            new Uri("https://example.com"),
            new VideoKey(0, "GetThumbnailPngStreamAsync", 100000),
            CancellationToken.None);

        // Assert
        using var image = Image.Load<Rgba32>(stream);
        await image.SaveAsPngAsync("../../../Out/output.png");
        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(1200));
            Assert.That(image.Height, Is.EqualTo(675));
        });
    }
}