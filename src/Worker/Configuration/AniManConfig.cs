namespace Bounan.Downloader.Worker.Configuration;

public record AniManConfig
{
    public static readonly string SectionName = "AniMan";

    /// <summary>
    /// Name of the Lambda function to get the next video to download.
    /// </summary>
    public required string GetVideoToDownloadLambdaFunctionName { get; init; }

    /// <summary>
    /// Name of the Lambda function to update the status of the video.
    /// </summary>
    public required string UpdateVideoStatusLambdaFunctionName { get; init; }

    /// <summary>
    /// URL of the notification queue.
    /// </summary>
    public required Uri NotificationQueueUrl { get; init; }
}