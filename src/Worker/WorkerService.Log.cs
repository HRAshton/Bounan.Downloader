namespace Bounan.Downloader.Worker;

public partial class WorkerService
{
    private static partial class Log
    {
        public static Func<ILogger, int, IDisposable?> BeginScopeWorkerId { get; }
            = LoggerMessage.DefineScope<int>("workerId={WorkerId}");

        public static Func<ILogger, string, IDisposable?> BeginScopeMsg { get; }
            = LoggerMessage.DefineScope<string>("msg={MessageHash}");

        [LoggerMessage(LogLevel.Information, "Worker running at: {Time}")]
        public static partial void WorkerRunning(ILogger logger, DateTimeOffset time);

        [LoggerMessage(LogLevel.Information, "Waiting for message")]
        public static partial void WaitingForMessage(ILogger logger);

        [LoggerMessage(LogLevel.Information, "Worker released")]
        public static partial void WorkerReleased(ILogger logger);

        [LoggerMessage(LogLevel.Information, "Video processed in {Elapsed}")]
        public static partial void VideoProcessed(ILogger logger, TimeSpan elapsed);
    }
}