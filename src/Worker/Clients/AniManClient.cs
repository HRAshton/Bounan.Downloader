using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Bounan.Common.Models;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Bounan.Downloader.Worker.Clients;

[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
public partial class AniManClient : IAniManClient, IDisposable
{
    private bool _disposedValue;
    private readonly SemaphoreSlim _semaphore = new (1, 1);

    public AniManClient(
        ILogger<AniManClient> logger,
        IOptions<AniManConfig> aniManConfig,
        IAmazonLambda lambdaClient)
    {
        LambdaClient = lambdaClient;
        Logger = logger;
        AniManConfig = aniManConfig;
    }

    private ILogger<AniManClient> Logger { get; }

    private IOptions<AniManConfig> AniManConfig { get; }

    private IAmazonLambda LambdaClient { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _semaphore.Dispose();
        }

        _disposedValue = true;
    }

    public async Task<DwnQueueResponse?> GetNextVideo(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var request = new InvokeRequest
            {
                FunctionName = AniManConfig.Value.GetVideoToDownloadLambdaFunctionName,
                InvocationType = InvocationType.RequestResponse
            };

            var response = await LambdaClient.InvokeAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Log.FailedToGetVideoInfo(Logger, response.HttpStatusCode);
                return null;
            }

            var payload = Encoding.UTF8.GetString(response.Payload.ToArray());
            return JsonConvert.DeserializeObject<DwnQueueResponse>(payload);
        }
        catch (Exception ex)
        {
            Log.FailedToGetVideoInfo(Logger, ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SendResult(IDwnResultNotification result, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var request = new InvokeRequest
            {
                FunctionName = AniManConfig.Value.UpdateVideoStatusLambdaFunctionName,
                InvocationType = InvocationType.RequestResponse,
                Payload = JsonConvert.SerializeObject(result)
            };

            var response = await LambdaClient.InvokeAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Log.FailedToSendResult(Logger, response.HttpStatusCode);
            }
        }
        catch (Exception ex)
        {
            Log.FailedToSendResult(Logger, ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}