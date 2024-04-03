using System;

namespace Bounan.Downloader.AwsCdk;

public class BounanCdkStackConfig
{
    public required string AlertEmail { get; init; }

    public required string LoanApiToken { get; init; }

    public required string GetVideoToDownloadLambdaFunctionName { get; init; }

    public required string UpdateVideoStatusLambdaFunctionName { get; init; }

    public required string NotificationQueueArn { get; init; }

    public required int WarmupTimeoutMinutes { get; init; } = 5;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(AlertEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(LoanApiToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(GetVideoToDownloadLambdaFunctionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(UpdateVideoStatusLambdaFunctionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(NotificationQueueArn);
        if (WarmupTimeoutMinutes <= 0)
        {
            throw new ArgumentException("WarmupTimeoutMinutes must be greater than 0");
        }
    }
}