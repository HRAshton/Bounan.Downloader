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
using Newtonsoft.Json.Serialization;

namespace Bounan.Downloader.Worker.Clients;

[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
public sealed partial class AniManClient(
    ILogger<AniManClient> logger,
    IOptions<AniManConfig> aniManConfig,
    IAmazonLambda lambdaClient)
    : IAniManClient, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    private ILogger<AniManClient> Logger { get; } = logger;

    private IOptions<AniManConfig> AniManConfig { get; } = aniManConfig;

    private IAmazonLambda LambdaClient { get; } = lambdaClient;

    public void Dispose()
    {
        _semaphore.Dispose();
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

            InvokeResponse response = await LambdaClient.InvokeAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Log.FailedToGetVideoInfo(Logger, response.HttpStatusCode);
                return null;
            }

            string payload = Encoding.UTF8.GetString(response.Payload.ToArray());
            return JsonConvert.DeserializeObject<DownloaderResponse>(payload, _jsonSerializerSettings);
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
                Payload = JsonConvert.SerializeObject(result, _jsonSerializerSettings),
            };

            InvokeResponse response = await LambdaClient.InvokeAsync(request, cancellationToken);
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