namespace Bounan.Downloader.Worker.Configuration;

public record SqsConfig
{
	public static readonly string SectionName = "SqsConfig";

	/// <summary>
	/// Number of threads to process messages in parallel.
	/// </summary>
	public int Threads { get; init; } = 1;

	/// <summary>
	/// Number of seconds to wait for a message to be processed before it is considered failed.
	/// It is recommended to set this value to a value lower than the message visibility timeout
	/// to avoid processing the same broken message multiple times.
	/// </summary>
	public int MessageTimeoutSeconds { get; init; } = 300;

	/// <summary>
	/// Maximum number of sequential errors before the service stops processing messages.
	/// </summary>
	public int MaxSequentialErrors { get; init; } = 3;

	/// <summary>
	/// Configuration for the SQS queues in priority order.
	/// </summary>
	public required ICollection<SqsQueueConfig> Queues { get; init; }
}