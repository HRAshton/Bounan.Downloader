﻿using Amazon.SQS;
using Amazon.SQS.Model;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Worker.Services;

public class SqsService : ISqsService
{
	private readonly ReceiveMessageRequest[] _receiveMessageRequests;
	private readonly SemaphoreSlim _semaphore;

	public SqsService(
		ILogger<SqsService> logger,
		IOptions<SqsConfig> sqsConfig,
		IAmazonSQS sqsClient)
	{
		SqsConfig = sqsConfig;
		Logger = logger;
		SqsClient = sqsClient;

		_receiveMessageRequests = SqsConfig.Value.Queues
			.Where(q => q.QueueUrl is not null)
			.Select(q => new ReceiveMessageRequest
			{
				QueueUrl = q.QueueUrl!.ToString(),
				MaxNumberOfMessages = 1,
				WaitTimeSeconds = q.PoolingWaitTimeSeconds,
			})
			.ToArray();

		if (_receiveMessageRequests.Length == 0)
		{
			throw new ArgumentException("No valid queues configured");
		}

		if (_receiveMessageRequests.Any(r => r.WaitTimeSeconds is < 1 or > 20))
		{
			throw new ArgumentException("Invalid pooling wait time");
		}

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
		var (response, queueIndex) = await ReceiveMessageAsync(cancellationToken);
		if (response is null)
		{
			return;
		}

		Logger.LogInformation(
			"Processing message: {MessageId} from queue {QueueIndex}",
			response.Messages[0].MessageId,
			queueIndex);

		var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(SqsConfig.Value.MessageTimeoutSeconds));
		var messageCancellationToken = cancellationTokenSource.Token;

		var message = response.Messages[0];
		await processMessage(message.Body, messageCancellationToken);

		Logger.LogInformation("Deleting message: {MessageId}", message.MessageId);

		await SqsClient.DeleteMessageAsync(
			_receiveMessageRequests[queueIndex].QueueUrl,
			message.ReceiptHandle,
			cancellationToken);
	}

	private async Task<(ReceiveMessageResponse? Response, int QueueIndex)> ReceiveMessageAsync(
		CancellationToken cancellationToken)
	{
		for (var queueIndex = 0; queueIndex < _receiveMessageRequests.Length; queueIndex++)
		{
			var response = await SqsClient.ReceiveMessageAsync(
				_receiveMessageRequests[queueIndex],
				cancellationToken);

			if (response.Messages.Count > 0)
			{
				return (response, queueIndex);
			}
		}

		return (null, -1);
	}
}