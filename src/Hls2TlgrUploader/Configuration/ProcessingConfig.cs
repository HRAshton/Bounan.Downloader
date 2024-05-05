namespace Bounan.Downloader.Hls2TlgrUploader.Configuration;

internal sealed record ProcessingConfig
{
    public const string SectionName = "Processing";

    /// <summary>
    /// Number of files to download concurrently.
    /// </summary>
    public int ConcurrentDownloads { get; init; } = 1;

    /// <summary>
    /// Pattern for temporary video files. {0} is replaced with a guid.
    /// </summary>
    public string TempVideoFilePattern { get; init; } = "video-{0}.mp4";
}