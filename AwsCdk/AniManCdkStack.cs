using System;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Configuration;
using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;

namespace Bounan.Downloader.AwsCdk;

public class AniManCdkStack : Stack
{
	internal AniManCdkStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
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
		GrantPermissionsForLambdas(config, user);

		var logGroup = CreateLogGroup();
		SetErrorAlarm(config, logGroup);
		logGroup.GrantWrite(user);

		var accessKey = new CfnAccessKey(this, "AccessKey", new CfnAccessKeyProps { UserName = user.UserName });

		Out("Bounan.Downloader.LogGroupName", logGroup.LogGroupName);
		Out("Bounan.Downloader.UserAccessKeyId", accessKey.Ref);
		Out("Bounan.Downloader.UserSecretAccessKey", accessKey.AttrSecretAccessKey);
	}

	private void GrantPermissionsForQueue(BounanCdkStackConfig config, User user)
	{
		var dwnNotificationsQueue = Queue.FromQueueArn(
			this,
			"DwnNotificationsSqsQueue",
			config.NotificationQueueArn);
		dwnNotificationsQueue.GrantConsumeMessages(user);
		dwnNotificationsQueue.GrantSendMessages(user);
	}

	private void GrantPermissionsForLambdas(BounanCdkStackConfig config, User user)
	{
		var getAnimeToDownloadLambda = Function.FromFunctionName(
			this,
			"LambdaHandlers.GetAnime",
			config.GetVideoToDownloadLambdaFunctionName);
		getAnimeToDownloadLambda.GrantInvoke(user);

		var updateVideoStatusLambda = Function.FromFunctionName(
			this,
			"LambdaHandlers.UpdateVideoStatus",
			config.UpdateVideoStatusLambdaFunctionName);
		updateVideoStatusLambda.GrantInvoke(user);
	}

	private LogGroup CreateLogGroup()
	{
		return new LogGroup(this, "LogGroup", new LogGroupProps
		{
			Retention = RetentionDays.ONE_WEEK
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
			MetricName = "ErrorCount"
		});

		_ = new Alarm(this, "LogGroupErrorAlarm", new AlarmProps
		{
			Metric = errorPattern.Metric(),
			Threshold = 1,
			EvaluationPeriods = 1,
			TreatMissingData = TreatMissingData.NOT_BREACHING,
		});
	}

	private void Out(string key, string value)
	{
		_ = new CfnOutput(this, key, new CfnOutputProps { Value = value });
	}
}