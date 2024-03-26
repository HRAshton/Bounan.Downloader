using Amazon.SQS;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Factories;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Services;
using Bounan.LoanApi;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace Bounan.Downloader.Worker;

public static class Bootstrap
{
	public static void RegisterServices(IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<SqsConfig>(configuration.GetSection(SqsConfig.SectionName));
		services.Configure<VideoServiceConfig>(configuration.GetSection(VideoServiceConfig.SectionName));
		services.Configure<TelegramConfig>(configuration.GetSection(TelegramConfig.SectionName));

		services.AddLogging(logging =>
		{
			logging.ClearProviders();
			logging.AddConsole();
			logging.AddAWSProvider(new AWSLoggerConfigSection(configuration.GetSection(AwsLoggerConfig.SectionName)));
		});

		Registrar.RegisterServices(services);
		Registrar.RegisterRefitClients(services);

		services.AddHttpClient();

		services.AddSingleton<IAmazonSQS, AmazonSQSClient>();

		services.AddSingleton<ISqsService, SqsService>();
		services.AddSingleton<IVideoCopyingService, VideoCopyingService>();
		services.AddSingleton<IFfmpegFactory, FfmpegFactory>();
		services.AddTransient<IFfmpegService, FfmpegService>();

		services.AddSingleton<ITelegramBotClient, TelegramBotClient>(provider =>
		{
			var telegramConfig = provider.GetRequiredService<IOptions<TelegramConfig>>().Value;
			var clientOptions = new TelegramBotClientOptions(telegramConfig.BotToken, telegramConfig.ApiUrl.ToString());
			return new TelegramBotClient(clientOptions);
		});

		services.AddHostedService<WorkerService>();
	}
}