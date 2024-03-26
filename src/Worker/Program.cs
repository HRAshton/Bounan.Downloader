using Amazon.SQS;
using Bounan.Downloader.Worker;
using Bounan.Downloader.Worker.Configuration;
using Bounan.Downloader.Worker.Factories;
using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Services;
using Bounan.LoanApi;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SqsConfig>(builder.Configuration.GetSection(SqsConfig.SectionName));
builder.Services.Configure<VideoServiceConfig>(builder.Configuration.GetSection(VideoServiceConfig.SectionName));
builder.Services.Configure<TelegramConfig>(builder.Configuration.GetSection(TelegramConfig.SectionName));

builder.Services.AddLogging(logging =>
{
	logging.ClearProviders();
	logging.AddConsole();
	logging.AddAWSProvider(new AWSLoggerConfigSection(builder.Configuration.GetSection(AwsLoggerConfig.SectionName)));
});

Registrar.RegisterServices(builder.Services);
Registrar.RegisterRefitClients(builder.Services);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();

#if DEBUG && false
AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.Always;
AWSConfigs.LoggingConfig.LogMetrics = true;
AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.JSON;
#endif

builder.Services.AddSingleton<ISqsService, SqsService>();
builder.Services.AddSingleton<IVideoCopyingService, VideoCopyingService>();
builder.Services.AddSingleton<IFfmpegFactory, FfmpegFactory>();
builder.Services.AddTransient<IFfmpegService, FfmpegService>();

builder.Services.AddSingleton<ITelegramBotClient, TelegramBotClient>(provider =>
{
	var telegramConfig = provider.GetRequiredService<IOptions<TelegramConfig>>().Value;
	var clientOptions = new TelegramBotClientOptions(telegramConfig.BotToken, telegramConfig.ApiUrl.ToString());
	return new TelegramBotClient(clientOptions);
});

builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();