namespace Bounan.Downloader.Worker.Configuration;

internal record ThumbnailConfig
{
    public const string SectionName = "Thumbnail";

    public bool ApplyWatermark { get; init; } = true;

    public required string BotId { get; init; }
}