namespace Bounan.Downloader.Worker.Configuration;

public record VideoServiceConfig
{
	public static readonly string SectionName = "VideoService";

	/// <summary>
	/// Number of files to download concurrently.
	/// </summary>
	public int ConcurrentDownloads { get; init; } = 1;

	/// <summary>
	/// Pattern for temporary video files. {0} is replaced with a guid.
	/// </summary>
	public string TempVideoFilePattern { get; init; } = "video-{0}.mp4";
}