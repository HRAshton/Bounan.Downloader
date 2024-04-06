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
    }
}