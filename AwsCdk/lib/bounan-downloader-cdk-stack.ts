import * as cdk from 'aws-cdk-lib';
import * as cloudwatch from 'aws-cdk-lib/aws-cloudwatch';
import * as cloudwatchActions from 'aws-cdk-lib/aws-cloudwatch-actions';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as sns from 'aws-cdk-lib/aws-sns';
import * as subscriptions from 'aws-cdk-lib/aws-sns-subscriptions';
import { CfnOutput } from 'aws-cdk-lib';
import { Construct } from 'constructs';
import { config } from '../config';

export class BounanDownloaderCdkStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props?: cdk.StackProps) {
        super(scope, id, props);

        const logGroup = new logs.LogGroup(this, 'bounan-downloader-log-group', {
            retention: logs.RetentionDays.ONE_WEEK,
        });

        const usersGroup = new iam.Group(this, 'bounan-downloader-users-group', {
            managedPolicies: [
                iam.ManagedPolicy.fromAwsManagedPolicyName('AmazonSQSFullAccess'),
            ],
        });

        logGroup.grantWrite(usersGroup);
        this.setupErrorAlarm(logGroup);

        new CfnOutput(this, 'LogGroupName', { value: logGroup.logGroupName });
    }

    private setupErrorAlarm(logGroup: logs.LogGroup) {
        const topic = new sns.Topic(this, 'LambdaErrorTopic');

        topic.addSubscription(new subscriptions.EmailSubscription(config.errorAlarmEmail));

        const errorFilter = new logs.MetricFilter(this, 'ErrorFilter', {
            logGroup: logGroup,
            metricNamespace: 'bounan-downloader-metrics',
            metricName: 'ErrorCount',
            filterPattern: logs.FilterPattern.anyTerm('error', 'exception', 'warn'),
            metricValue: '1',
        });

        const alarm = new cloudwatch.Alarm(this, 'Alarm', {
            metric: errorFilter.metric(),
            threshold: 1,
            evaluationPeriods: 1,
            alarmDescription: 'Alarm when the Lambda function logs errors.',
        });

        alarm.addAlarmAction(new cloudwatchActions.SnsAction(topic));
    }
}
