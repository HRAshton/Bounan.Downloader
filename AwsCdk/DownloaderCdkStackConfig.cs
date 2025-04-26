using Amazon.CDK;
using Amazon.CDK.AWS.SSM;
using Microsoft.Extensions.Configuration;

namespace Bounan.Downloader.AwsCdk;

public class DownloaderCdkStackConfig
{
    public DownloaderCdkStackConfig(Stack stack, string cdkPrefix, string ssmPrefix)
    {
        var localConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        AlertEmail = GetCdkValue(cdkPrefix, "alert-email", localConfig);
        LoanApiToken = GetCdkValue(cdkPrefix, "loan-api-token", localConfig);

        GetVideoToDownloadLambdaName = GetCdkValue(cdkPrefix, "get-video-to-download", localConfig);
        UpdateVideoStatusLambdaName = GetCdkValue(cdkPrefix, "update-video-status", localConfig);
        VideoRegisteredTopicArn = GetCdkValue(cdkPrefix, "video-registered-sns-topic-arn", localConfig);

        UploadBotToken = GetSsmValue(stack, nameof(UploadBotToken), ssmPrefix);
        UploadDestinationChatId = GetSsmValue(stack, nameof(UploadDestinationChatId), ssmPrefix);
        TelegramAppId = GetSsmValue(stack, nameof(TelegramAppId), ssmPrefix);
        TelegramAppHash = GetSsmValue(stack, nameof(TelegramAppHash), ssmPrefix);
        ThumbnailBotUsername = GetSsmValue(stack, nameof(ThumbnailBotUsername), ssmPrefix);
    }

    public string AlertEmail { get; }

    public string LoanApiToken { get; }

    public string GetVideoToDownloadLambdaName { get; }

    public string UpdateVideoStatusLambdaName { get; }

    public string VideoRegisteredTopicArn { get; }

    public string UploadBotToken { get; }

    public string UploadDestinationChatId { get; }

    public string TelegramAppId { get; }

    public string TelegramAppHash { get; }

    public string ThumbnailBotUsername { get; }

    private static string GetCdkValue(string cdkPrefix, string key, IConfigurationRoot localConfig)
    {
        string? localValue = localConfig.GetValue<string>(key);
        return localValue is { Length: > 0 } ? localValue : Fn.ImportValue(cdkPrefix + key);
    }

    private static string GetSsmValue(Stack stack, string key, string ssmPrefix)
    {
        return StringParameter.ValueForStringParameter(stack, ssmPrefix + key);
    }
}