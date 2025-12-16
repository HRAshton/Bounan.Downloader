using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Constructs;
using Newtonsoft.Json;
using AlarmActions = Amazon.CDK.AWS.CloudWatch.Actions;
using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;
using Policy = Amazon.CDK.AWS.IAM.Policy;

namespace Bounan.Downloader.AwsCdk;

internal sealed class DownloaderCdkStack : Stack
{
    private const string RuntimeConfigParameterPrefix = "/bounan/downloader/runtime-config";

    internal DownloaderCdkStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        var config = new DownloaderCdkStackConfig(this, "bounan:", "/bounan/downloader/deploy-config/");

        var user = new User(this, "User");

        var image = BuildAndPushWorkerImage(user);

        var videoRegisteredQueue = CreateVideoRegisteredQueue(config, user);
        GrantPermissionsForLambdas(config, user);

        var logGroup = CreateLogGroup();
        SetErrorAlarm(config, logGroup);
        SetNoLogsAlarm(config, logGroup);
        logGroup.GrantWrite(user);

        var accessKey = new CfnAccessKey(this, "AccessKey", new CfnAccessKeyProps { UserName = user.UserName });

        SaveParameter(logGroup, videoRegisteredQueue, config, user);

        Out("Config", JsonConvert.SerializeObject(config));
        Out(
            "dotenv",
            $"WORKER_IMAGE_URI={image.ImageUri};\n"
            + $"AWS_REGION={Region};\n"
            + $"AWS_ACCESS_KEY_ID={accessKey.Ref};\n"
            + $"AWS_SECRET_ACCESS_KEY={accessKey.AttrSecretAccessKey};\n"
            + $"TELEGRAM_API_ID={config.TelegramAppId};\n"
            + $"TELEGRAM_API_HASH={config.TelegramAppHash};");
    }

    private DockerImageAsset BuildAndPushWorkerImage(IGrantable user)
    {
        var dockerImage = new DockerImageAsset(this, "WorkerImage", new DockerImageAssetProps { Directory = "." });
        dockerImage.Repository.GrantPull(user);

        return dockerImage;
    }

    private Queue CreateVideoRegisteredQueue(DownloaderCdkStackConfig config, IGrantable user)
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
            config.GetVideoToDownloadLambdaName);
        getAnimeToDownloadLambda.GrantInvoke(user);

        var updateVideoStatusLambda = Function.FromFunctionName(
            this,
            "UpdateVideoStatus",
            config.UpdateVideoStatusLambdaName);
        updateVideoStatusLambda.GrantInvoke(user);
    }

    private LogGroup CreateLogGroup()
    {
        return new LogGroup(this, "LogGroup", new LogGroupProps { Retention = RetentionDays.ONE_WEEK });
    }

    private void SetErrorAlarm(DownloaderCdkStackConfig config, LogGroup logGroup)
    {
        var topic = new Topic(this, "LogGroupAlarmSnsTopic", new TopicProps());

        topic.AddSubscription(new EmailSubscription(config.AlertEmail));

        var metricFilter = logGroup.AddMetricFilter("ErrorMetricFilter", new MetricFilterOptions
        {
            FilterPattern = FilterPattern.AnyTerm("[Error]"),
            MetricNamespace = StackName,
            MetricName = "ErrorCount",
            MetricValue = "1",
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

    private void SetNoLogsAlarm(DownloaderCdkStackConfig config, LogGroup logGroup)
    {
        var noLogsMetric = new Metric(new MetricProps
        {
            Namespace = "AWS/Logs",
            MetricName = "IncomingLogEvents",
            DimensionsMap = new Dictionary<string, string>
            {
                { "LogGroupName", logGroup.LogGroupName },
            },
            Statistic = "Sum",
            Period = Duration.Minutes(2),
        });

        var noLogAlarm = new Alarm(this, "NoLogsAlarm", new AlarmProps
        {
            Metric = noLogsMetric,
            Threshold = 0,
            ComparisonOperator = ComparisonOperator.LESS_THAN_OR_EQUAL_TO_THRESHOLD,
            EvaluationPeriods = 1,
            TreatMissingData = TreatMissingData.BREACHING,
            AlarmDescription = "Alarm if no logs received within 2 minutes",
        });

        var topic = new Topic(this, "NoLogAlarmSnsTopic", new TopicProps());
        topic.AddSubscription(new EmailSubscription(config.AlertEmail));
        noLogAlarm.AddAlarmAction(new AlarmActions.SnsAction(topic));
    }

    private void SaveParameter(
        LogGroup logGroup,
        Queue videoRegisteredQueue,
        DownloaderCdkStackConfig config,
        User user)
    {
        var runtimeConfig = new
        {
            AniMan = new
            {
                GetVideoToDownloadLambdaFunctionName = config.GetVideoToDownloadLambdaName,
                UpdateVideoStatusLambdaFunctionName = config.UpdateVideoStatusLambdaName,
            },
            Logging = new
            {
                LogGroup = logGroup.LogGroupName,
            },
            Sqs = new
            {
                NotificationQueueUrl = videoRegisteredQueue.QueueUrl,
            },
            Hls2TlgrUploader = new
            {
                Telegram = new
                {
                    BotToken = config.UploadBotToken,
                    DestinationChatId = config.UploadDestinationChatId,
                },
            },
            Thumbnail = new
            {
                BotId = config.ThumbnailBotUsername,
            },
            Processing = new
            {
                Threads = 2,
                TimeoutSeconds = 600,
                UseLowestQuality = false,
            },
            LoanApi = new
            {
                Token = config.LoanApiToken,
            },
        };

        string json = JsonConvert.SerializeObject(runtimeConfig, Formatting.Indented);

        _ = new StringParameter(this, "runtime-config", new StringParameterProps
        {
            ParameterName = RuntimeConfigParameterPrefix + "/json",
            StringValue = json,
        });

        user.AttachInlinePolicy(new Policy(
            this,
            "ParameterPolicy",
            new PolicyProps
            {
                Statements =
                [
                    new PolicyStatement(new PolicyStatementProps
                    {
                        Actions =
                        [
                            "ssm:GetParametersByPath",
                            "ssm:DescribeParameters",
                            "ssm:GetParameter",
                            "ssm:GetParameterHistory",
                            "ssm:GetParameters",
                        ],
                        Resources =
                        [
                            $"arn:aws:ssm:{Region}:{Account}:parameter{RuntimeConfigParameterPrefix}/*",
                        ],
                    }),
                ],
            }));
    }

    private void Out(string key, string value)
    {
        _ = new CfnOutput(this, key, new CfnOutputProps { Value = value });
    }
}
