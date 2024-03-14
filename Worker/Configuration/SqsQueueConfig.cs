namespace Bounan.Downloader.Worker.Configuration;

public class SqsQueueConfig
{
	/// <summary>
	/// Queue URL. Skip processing the queue if not set.
	/// </summary>
	public Uri? QueueUrl { get; init; }

	/// <summary>
	/// Number of seconds to wait for a message to be available in the queue.
	/// </summary>
	public int PoolingWaitTimeSeconds { get; init; } = 20;
}