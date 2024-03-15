using JetBrains.Annotations;

namespace Bounan.Downloader.Worker.Configuration;

public record SqsQueueConfig
{
	/// <summary>
	/// Queue URL.
	/// </summary>
	public Uri? QueueUrl { get; [UsedImplicitly] init; }

	/// <summary>
	/// Number of seconds to wait for a message to be available in the queue.
	/// </summary>
	public int PoolingWaitTimeSeconds { get; [UsedImplicitly] init; } = 20;
}