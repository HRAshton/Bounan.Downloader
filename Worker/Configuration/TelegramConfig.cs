namespace Bounan.Downloader.Worker.Configuration;

public record TelegramConfig
{
	public static readonly string SectionName = "Telegram";

	public required string BotToken { get; init; }

	public required string DestinationChatId { get; init; }
	
	public Uri ApiUrl { get; init; } = new ("https://api.telegram.org");
}