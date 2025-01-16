namespace Bounan.Downloader.Worker.Configuration;

internal record ThumbnailConfig
{
    public const string SectionName = "Thumbnail";

    public required string BotId { get; init; }
}