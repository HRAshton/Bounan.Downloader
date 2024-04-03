using System;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Configuration;
using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;
using Targets = Amazon.CDK.AWS.Events.Targets;

namespace Bounan.Downloader.AwsCdk;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This is a stack.")]
public class AniManCdkStack : Stack
{
    internal AniManCdkStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build()
            .Get<BounanCdkStackConfig>();
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        config.Validate();

        var user = new User(this, "User");

        GrantPermissionsForQueue(config, user);

        var (getAnimeToDownloadLambda, updateVideoStatusLambda) = GrantPermissionsForLambdas(config);
        getAnimeToDownloadLambda.GrantInvoke(user);
        updateVideoStatusLambda.GrantInvoke(user);

        CreateWarmer(config, getAnimeToDownloadLambda);

        var logGroup = CreateLogGroup();
        SetErrorAlarm(config, logGroup);
        logGroup.GrantWrite(user);

        var accessKey = new CfnAccessKey(this, "AccessKey", new CfnAccessKeyProps { UserName = user.UserName });

        Out("Bounan.Downloader.LogGroupName", logGroup.LogGroupName);
        Out("Bounan.Downloader.UserAccessKeyId", accessKey.Ref);
        Out("Bounan.Downloader.UserSecretAccessKey", accessKey.AttrSecretAccessKey);
    }

    private void GrantPermissionsForQueue(BounanCdkStackConfig config, IGrantable user)
    {
        var dwnNotificationsQueue = Queue.FromQueueArn(
            this,
            "DwnNotificationsSqsQueue",
            config.NotificationQueueArn);
        dwnNotificationsQueue.GrantConsumeMessages(user);
        dwnNotificationsQueue.GrantSendMessages(user);
    }

    private (IFunction GetAnimeToDownloadLambda, IFunction UpdateVideoStatusLambda) GrantPermissionsForLambdas(
        BounanCdkStackConfig config)
    {
        var getAnimeToDownloadLambda = Function.FromFunctionName(
            this,
            "LambdaHandlers.GetAnime",
            config.GetVideoToDownloadLambdaFunctionName);

        var updateVideoStatusLambda = Function.FromFunctionName(
            this,
            "LambdaHandlers.UpdateVideoStatus",
            config.UpdateVideoStatusLambdaFunctionName);

        return (getAnimeToDownloadLambda, updateVideoStatusLambda);
    }

    private LogGroup CreateLogGroup()
    {
        return new LogGroup(this, "LogGroup", new LogGroupProps
        {
            Retention = RetentionDays.ONE_WEEK,
        });
    }

    private void SetErrorAlarm(BounanCdkStackConfig bounanCdkStackConfig, ILogGroup logGroup)
    {
        var topic = new Topic(this, "LogGroupAlarmSnsTopic", new TopicProps());

        topic.AddSubscription(new EmailSubscription(bounanCdkStackConfig.AlertEmail));

        var errorPattern = new MetricFilter(this, "LogGroupErrorPattern", new MetricFilterProps
        {
            LogGroup = logGroup,
            FilterPattern = FilterPattern.AnyTerm("ERROR", "Error", "error"),
            MetricNamespace = "MetricNamespace",
            MetricName = "ErrorCount",
        });

        _ = new Alarm(this, "LogGroupErrorAlarm", new AlarmProps
        {
            Metric = errorPattern.Metric(),
            Threshold = 1,
            EvaluationPeriods = 1,
            TreatMissingData = TreatMissingData.NOT_BREACHING,
        });
    }

    private void CreateWarmer(BounanCdkStackConfig bounanCdkStackConfig, IFunction webhookHandler)
    {
        var rule = new Rule(this, "WarmerRule", new RuleProps
        {
            Schedule = Schedule.Rate(Duration.Minutes(bounanCdkStackConfig.WarmupTimeoutMinutes)),
        });

        rule.AddTarget(new Targets.LambdaFunction(webhookHandler));
    }

    private void Out(string key, string value)
    {
        _ = new CfnOutput(this, key, new CfnOutputProps { Value = value });
    }
}