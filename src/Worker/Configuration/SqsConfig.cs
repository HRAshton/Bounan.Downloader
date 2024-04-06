namespace Bounan.Downloader.Worker.Configuration;

public record SqsConfig
{
	public static readonly string SectionName = "Sqs";

	/// <summary>
	/// Number of seconds to wait for a message.
	/// </summary>
	public int PollingIntervalSeconds { get; init; } = 20;

	/// <summary>
	/// Number of seconds to wait before retrying after an error.
	/// </summary>
    public int ErrorRetryIntervalSeconds { get; init; } = 5;
}