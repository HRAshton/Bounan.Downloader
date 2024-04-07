using Amazon.Lambda;
using Amazon.SQS;
using Bounan.Downloader.Worker.Clients;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Factories;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Services;
using Microsoft.Extensions.Options;
using Telegram.Bot;
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
        services.Configure<VideoServiceConfig>(configuration.GetSection(VideoServiceConfig.SectionName));
        services.Configure<TelegramConfig>(configuration.GetSection(TelegramConfig.SectionName));
        services.Configure<ProcessingConfig>(configuration.GetSection(ProcessingConfig.SectionName));

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole();
            logging.AddAWSProvider(configuration.GetAWSLoggingConfigSection());
        });

        LoanApi.Registrar.RegisterConfiguration(services, configuration);
        LoanApi.Registrar.RegisterDownloaderServices(services);

        services.AddHttpClient();

        var awsOptions = configuration.GetAWSOptions();
        services.AddSingleton<IAmazonLambda>(_ => awsOptions.CreateServiceClient<IAmazonLambda>());
        services.AddSingleton<IAmazonSQS>(_ => awsOptions.CreateServiceClient<IAmazonSQS>());
        services.AddSingleton<IAniManClient, AniManClient>();

        services.AddSingleton<ISqsClient, SqsClient>();
        services.AddSingleton<IVideoCopyingService, VideoCopyingService>();
        services.AddSingleton<IVideoMergingService, VideoMergingService>();
        services.AddSingleton<IFfmpegFactory, FfmpegFactory>();

        services.AddTransient<IFfmpegService, FfmpegService>();

        services.AddSingleton<ITelegramBotClient, TelegramBotClient>(provider =>
        {
            var telegramConfig = provider.GetRequiredService<IOptions<TelegramConfig>>().Value;
            var clientOptions = new TelegramBotClientOptions(telegramConfig.BotToken, telegramConfig.ApiUrl.ToString());
            var defaultClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(telegramConfig.TimeoutSeconds)
            };

            return new TelegramBotClient(clientOptions, defaultClient);
        });

        services.AddHostedService<WorkerService>();
    }
}