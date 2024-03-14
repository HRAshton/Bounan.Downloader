using Amazon.SQS;
using Amazon.SQS.Model;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Worker.Services;

public class SqsService : ISqsService
{
	private readonly ReceiveMessageRequest _receiveMessageRequest;
	private readonly SemaphoreSlim _semaphore;

	public SqsService(
		ILogger<SqsService> logger,
		IOptions<SqsConfig> sqsConfig,
		IAmazonSQS sqsClient)
	{
		SqsConfig = sqsConfig;
		Logger = logger;
		SqsClient = sqsClient;

		_receiveMessageRequest = new ReceiveMessageRequest
		{
			QueueUrl = sqsConfig.Value.QueueUrl.ToString(),
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = SqsConfig.Value.PoolingWaitTimeSeconds,
		};

		_semaphore = new SemaphoreSlim(sqsConfig.Value.Threads, sqsConfig.Value.Threads);
	}

	private IOptions<SqsConfig> SqsConfig { get; }

	private ILogger<SqsService> Logger { get; }

	private IAmazonSQS SqsClient { get; }

	public async Task StartProcessing(
		Func<string, CancellationToken, Task> processMessageFn,
		CancellationToken cancellationToken)
	{
		var sequentialErrors = 0;
		while (!cancellationToken.IsCancellationRequested && sequentialErrors < SqsConfig.Value.MaxSequentialErrors)
		{
			await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

			_ = Task.Run(
				async () =>
				{
					try
					{
						await TryProcessNextMessage(processMessageFn, cancellationToken);
						Interlocked.Exchange(ref sequentialErrors, 0);
					}
					catch (Exception ex)
					{
						Interlocked.Increment(ref sequentialErrors);
						Logger.LogError(ex, "Error processing message");
						Logger.LogError(ex.InnerException, "Error processing message");
						await Task.Delay(5000, cancellationToken);
					}
					finally
					{
						_semaphore.Release();
					}
				},
				cancellationToken);
		}
	}

	private async Task TryProcessNextMessage(
		Func<string, CancellationToken, Task> processMessage,
		CancellationToken cancellationToken)
	{
		var response = await SqsClient.ReceiveMessageAsync(_receiveMessageRequest, cancellationToken);
		if (response.Messages.Count <= 0)
		{
			return;
		}

		Logger.LogInformation("Processing message: {MessageId}", response.Messages[0].MessageId);

		var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(SqsConfig.Value.MessageTimeoutSeconds));
		var messageCancellationToken = cancellationTokenSource.Token;

		var message = response.Messages[0];
		await processMessage(message.Body, messageCancellationToken);

		Logger.LogInformation("Deleting message: {MessageId}", message.MessageId);

		await SqsClient.DeleteMessageAsync(
			SqsConfig.Value.QueueUrl.ToString(),
			message.ReceiptHandle,
			cancellationToken);
	}
}