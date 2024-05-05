namespace Bounan.Downloader.Worker.Configuration;

public record ProcessingConfig
{
    public static readonly string SectionName = "Processing";

    /// <summary>
    /// Timeout in seconds for each video.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 6 * 60;

    /// <summary>
    /// Use the lowest quality available.
    /// For debugging purposes only.
    /// </summary>
    public bool UseLowestQuality { get; init; }

    /// <summary>
    /// Number of threads to process in parallel.
    /// </summary>
    public int Threads { get; init; } = 1;
}