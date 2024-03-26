namespace Bounan.Downloader.Worker.Factories;

public partial class FfmpegFactory
{
	private static partial class Log
	{
		[LoggerMessage(LogLevel.Debug, "Ffmpeg service created")]
		public static partial void FfmpegServiceCreated(ILogger logger);
	}
}