namespace Bounan.Downloader.Worker.Configuration;

public record ProcessingConfig
{
    public static readonly string SectionName = "Processing";

    /// <summary>
    /// Number of threads to process in parallel.
    /// </summary>
    public int Threads { get; init; } = 1;
}