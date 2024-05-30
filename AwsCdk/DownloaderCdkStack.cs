using System;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using AlarmActions = Amazon.CDK.AWS.CloudWatch.Actions;
using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;

namespace Bounan.Downloader.AwsCdk;

[SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
public class DownloaderCdkStack : Stack
{
    internal DownloaderCdkStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build()
            .Get<DownloaderCdkStackConfig>();
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        config.Validate();

        var user = new User(this, "User");

        var image = BuildAndPushWorkerImage(user);

        var videoRegisteredQueue = CreateVideoRegisteredQueue(config, user);
        GrantPermissionsForLambdas(config, user);

        var logGroup = CreateLogGroup();
        SetErrorAlarm(config, logGroup);
        logGroup.GrantWrite(user);

        var accessKey = new CfnAccessKey(this, "AccessKey", new CfnAccessKeyProps { UserName = user.UserName });

        Out("Config", JsonConvert.SerializeObject(config));
        Out("LogGroupName", logGroup.LogGroupName);
        Out("UserAccessKeyId", accessKey.Ref);
        Out("UserSecretAccessKey", accessKey.AttrSecretAccessKey);
        Out("VideoRegisteredQueueUrl", videoRegisteredQueue.QueueUrl);
        Out("ImageUri", image.ImageUri);
        
        Out(
            ".env",
            $"""
             AWS_REGION={Region}
             WORKER_IMAGE_URI={image.ImageUri}
             AWS_ACCESS_KEY_ID={accessKey.Ref}
             AWS_SECRET_ACCESS_KEY={accessKey.AttrSecretAccessKey}
             AniMan__GetVideoToDownloadLambdaFunctionName={config.GetVideoToDownloadLambdaFunctionName}
             AniMan__UpdateVideoStatusLambdaFunctionName={config.UpdateVideoStatusLambdaFunctionName}
             Logging__LogGroup={logGroup.LogGroupName}
             Sqs__NotificationQueueUrl={videoRegisteredQueue.QueueUrl}
             Hls2TlgrUploader__Telegram__BotToken=
             Hls2TlgrUploader__Telegram__DestinationChatId=
             LoanApi__Token=
             Processing__Threads=2
             Processing__TimeoutSeconds=600
             TELEGRAM_API_ID=
             TELEGRAM_API_HASH=
             Thumbnail__BotId=
             """);
    }

    private DockerImageAsset BuildAndPushWorkerImage(IGrantable user)
    {
        var dockerImage = new DockerImageAsset(this, "WorkerImage", new DockerImageAssetProps { Directory = "." });
        dockerImage.Repository.GrantPull(user);

        return dockerImage;
    }

    private IQueue CreateVideoRegisteredQueue(DownloaderCdkStackConfig config, IGrantable user)
    {
        var newEpisodesTopic = Topic.FromTopicArn(this, "VideoRegisteredTopic", config.VideoRegisteredTopicArn);
        var newEpisodesQueue = new Queue(this, "VideoRegisteredQueue");
        newEpisodesTopic.AddSubscription(new SqsSubscription(newEpisodesQueue));

        newEpisodesQueue.GrantConsumeMessages(user);

        return newEpisodesQueue;
    }

    private void GrantPermissionsForLambdas(DownloaderCdkStackConfig config, IGrantable user)
    {
        var getAnimeToDownloadLambda = Function.FromFunctionName(
            this,
            "GetAnime",
            config.GetVideoToDownloadLambdaFunctionName);
        getAnimeToDownloadLambda.GrantInvoke(user);

        var updateVideoStatusLambda = Function.FromFunctionName(
            this,
            "UpdateVideoStatus",
            config.UpdateVideoStatusLambdaFunctionName);
        updateVideoStatusLambda.GrantInvoke(user);
    }

    private ILogGroup CreateLogGroup()
    {
        return new LogGroup(this, "LogGroup", new LogGroupProps { Retention = RetentionDays.ONE_WEEK });
    }

    private void SetErrorAlarm(DownloaderCdkStackConfig config, ILogGroup logGroup)
    {
        var topic = new Topic(this, "LogGroupAlarmSnsTopic", new TopicProps());

        topic.AddSubscription(new EmailSubscription(config.AlertEmail));

        var metricFilter = logGroup.AddMetricFilter("ErrorMetricFilter", new MetricFilterOptions
        {
            FilterPattern = FilterPattern.AnyTerm("ERROR", "Error", "error", "fail"),
            MetricNamespace = StackName,
            MetricName = "ErrorCount",
            MetricValue = "1"
        });

        var alarm = new Alarm(this, "LogGroupErrorAlarm", new AlarmProps
        {
            Metric = metricFilter.Metric(),
            Threshold = 1,
            EvaluationPeriods = 1,
            TreatMissingData = TreatMissingData.NOT_BREACHING,
        });
        alarm.AddAlarmAction(new AlarmActions.SnsAction(topic));
    }

    private void Out(string key, string value)
    {
        _ = new CfnOutput(this, key, new CfnOutputProps { Value = value });
    }
}