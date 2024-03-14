namespace Bounan.Downloader.Worker.Configuration;

public record TelegramConfig
{
	public static readonly string SectionName = "Telegram";

	public required string BotToken { get; init; }

	public required string DestinationChatId { get; init; }
	
	public string ApiUrl { get; init; } = "https://api.telegram.org";
}