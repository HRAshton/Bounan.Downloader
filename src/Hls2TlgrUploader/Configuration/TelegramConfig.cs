namespace Bounan.Downloader.Hls2TlgrUploader.Configuration;

internal sealed record TelegramConfig
{
    public const string SectionName = "Telegram";

    public required string BotToken { get; init; }

    public required string DestinationChatId { get; init; }

    public Uri ApiUrl { get; init; } = new ("https://api.telegram.org");

    public int TimeoutSeconds { get; init; } = 10 * 60;
}