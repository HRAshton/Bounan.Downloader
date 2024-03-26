namespace Bounan.Downloader.Worker;

public partial class WorkerService
{
	private static partial class Log
	{
		[LoggerMessage(LogLevel.Information, "Worker running at: {Time}")]
		public static partial void WorkerRunning(ILogger logger, DateTimeOffset time);
	}
}