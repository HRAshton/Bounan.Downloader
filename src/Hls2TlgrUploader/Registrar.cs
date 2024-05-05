using Bounan.Downloader.Hls2TlgrUploader.Configuration;
using Bounan.Downloader.Hls2TlgrUploader.Interfaces;
using Bounan.Downloader.Hls2TlgrUploader.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using FfmpegFactory = Bounan.Downloader.Hls2TlgrUploader.Factories.FfmpegFactory;
using FfmpegService = Bounan.Downloader.Hls2TlgrUploader.Services.FfmpegService;
using VideoMergingService = Bounan.Downloader.Hls2TlgrUploader.Services.VideoMergingService;

namespace Bounan.Downloader.Hls2TlgrUploader;

public static class Registrar
{
    public static IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ProcessingConfig>(configuration.GetSection(ProcessingConfig.SectionName));
        services.Configure<TelegramConfig>(configuration.GetSection(TelegramConfig.SectionName));

        services.AddSingleton<IVideoUploadingService, VideoUploadingService>();
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

        return services;
    }
}