using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Amazon.SQS.Model;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Microsoft.Extensions.Options;

namespace Bounan.Downloader.Worker.Clients;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global - This class is partially inherited
public partial class SqsClient : ISqsClient, IDisposable
{
    private bool _isDisposed;
    private readonly int _errorRetryIntervalMs;
    private readonly ReceiveMessageRequest _receiveMessageRequest;
    private readonly SemaphoreSlim _semaphore;

    public SqsClient(
        ILogger<SqsClient> logger,
        IOptions<SqsConfig> sqsConfig,
        IOptions<ProcessingConfig> processingConfig,
        IAmazonSQS amazonSqs)
    {
        ArgumentNullException.ThrowIfNull(sqsConfig);
        ArgumentNullException.ThrowIfNull(processingConfig);

        Logger = logger;
        AmazonAmazonSqs = amazonSqs;

        _errorRetryIntervalMs = sqsConfig.Value.ErrorRetryIntervalSeconds * 1000;
        _receiveMessageRequest = new ReceiveMessageRequest
        {
            QueueUrl = sqsConfig.Value.NotificationQueueUrl.ToString(),
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = sqsConfig.Value.PollingIntervalSeconds
        };

        _semaphore = new SemaphoreSlim(processingConfig.Value.Threads, processingConfig.Value.Threads);
    }

    private ILogger<SqsClient> Logger { get; }

    private IAmazonSQS AmazonAmazonSqs { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _semaphore.Dispose();
        }

        _isDisposed = true;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public async Task WaitForMessageAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_receiveMessageRequest.WaitTimeSeconds);
        ArgumentNullException.ThrowIfNull(cancellationToken);
        Log.WaitingForMessage(Logger);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Prevent the message receiving operation from hanging indefinitely
                    using var hangPreventerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    hangPreventerCts.CancelAfter((_receiveMessageRequest.WaitTimeSeconds.Value + 2) * 1000);
                    var hangPreventer = hangPreventerCts.Token;

                    var response = await AmazonAmazonSqs.ReceiveMessageAsync(_receiveMessageRequest, hangPreventer);
                    Log.ReceivedMessages(Logger, response.Messages.Count);
                    if (response.Messages.Count <= 0) continue;

                    _ = AmazonAmazonSqs.DeleteMessageAsync(
                        new DeleteMessageRequest
                        {
                            QueueUrl = _receiveMessageRequest.QueueUrl,
                            ReceiptHandle = response.Messages[0].ReceiptHandle
                        },
                        cancellationToken);

                    Log.RunningVideoProcessing(Logger);
                    return;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Log.HangDetected(Logger);
                }
                catch (Exception ex)
                {
                    Log.FailedToReceiveMessage(Logger, ex.Message);
                    await Task.Delay(_errorRetryIntervalMs, cancellationToken);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}