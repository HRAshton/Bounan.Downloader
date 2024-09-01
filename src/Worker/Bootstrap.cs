using Amazon.Lambda;
using Amazon.SQS;
using Bounan.Downloader.Worker.Clients;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Services;
using Hls2TlgrUploader;
using SqsClient = Bounan.Downloader.Worker.Clients.SqsClient;

namespace Bounan.Downloader.Worker;

public static class Bootstrap
{
    public static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AniManConfig>(configuration.GetSection(AniManConfig.SectionName));
        services.Configure<SqsConfig>(configuration.GetSection(SqsConfig.SectionName));
        services.Configure<ProcessingConfig>(configuration.GetSection(ProcessingConfig.SectionName));
        services.Configure<ThumbnailConfig>(configuration.GetSection(ThumbnailConfig.SectionName));

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole();
            logging.AddAWSProvider(configuration.GetAWSLoggingConfigSection());
        });

        LoanApi.Registrar.RegisterConfiguration(services, configuration);
        LoanApi.Registrar.RegisterDownloaderServices(services);
        services.AddHls2TlgrUploader(configuration.GetRequiredSection("Hls2TlgrUploader"));

        services.AddHttpClient();

        var awsOptions = configuration.GetAWSOptions();
        services.AddSingleton<IAmazonLambda>(_ => awsOptions.CreateServiceClient<IAmazonLambda>());
        services.AddSingleton<IAmazonSQS>(_ => awsOptions.CreateServiceClient<IAmazonSQS>());
        services.AddSingleton<IAniManClient, AniManClient>();

        services.AddSingleton<ISqsClient, SqsClient>();
        services.AddSingleton<IVideoCopyingService, VideoCopyingService>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();

        services.AddHostedService<WorkerService>();
    }
}