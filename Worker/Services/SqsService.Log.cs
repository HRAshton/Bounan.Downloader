namespace Bounan.Downloader.Worker.Services;

public partial class SqsService
{
	private static partial class Log
	{
		public static Func<ILogger, ulong, IDisposable?> BeginScope { get; }
			= LoggerMessage.DefineScope<ulong>("msg={MessageHash}");

		[LoggerMessage(LogLevel.Debug, "Disposing SqsService")]
		public static partial void DisposingSqsService(ILogger logger);

		[LoggerMessage(LogLevel.Error, "Error while processing message: {ErrorMessage}")]
		public static partial void ErrorProcessingMessage(ILogger logger, string errorMessage);

		[LoggerMessage(LogLevel.Information, "Processing message {Message} from queue {QueueIndex}")]
		public static partial void ProcessingMessage(ILogger logger, string message, int queueIndex);

		[LoggerMessage(LogLevel.Information, "Deleting message: {MessageId}")]
		public static partial void DeletingMessage(ILogger logger, string messageId);
	}
}