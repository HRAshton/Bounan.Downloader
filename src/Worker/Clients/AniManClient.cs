using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Bounan.Common;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Bounan.Downloader.Worker.Clients;

[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
public partial class AniManClient(
    ILogger<AniManClient> logger,
    IOptions<AniManConfig> aniManConfig,
    IAmazonLambda lambdaClient)
    : IAniManClient, IDisposable
{
    private bool _disposedValue;
    private readonly SemaphoreSlim _semaphore = new (1, 1);

    private ILogger<AniManClient> Logger { get; } = logger;

    private IOptions<AniManConfig> AniManConfig { get; } = aniManConfig;

    private IAmazonLambda LambdaClient { get; } = lambdaClient;

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

    public async Task<DownloaderResponse?> GetNextVideo(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var request = new InvokeRequest
            {
                FunctionName = AniManConfig.Value.GetVideoToDownloadLambdaFunctionName,
                InvocationType = InvocationType.RequestResponse,
            };

            var response = await LambdaClient.InvokeAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Log.FailedToGetVideoInfo(Logger, response.HttpStatusCode);
                return null;
            }

            string payload = Encoding.UTF8.GetString(response.Payload.ToArray());
            return JsonConvert.DeserializeObject<DownloaderResponse>(payload);
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

    public async Task SendResult(DownloaderResultRequest result, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var request = new InvokeRequest
            {
                FunctionName = AniManConfig.Value.UpdateVideoStatusLambdaFunctionName,
                InvocationType = InvocationType.RequestResponse,
                Payload = JsonConvert.SerializeObject(result),
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