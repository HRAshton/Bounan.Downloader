namespace Bounan.Downloader.Worker;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        Bootstrap.RegisterServices(builder.Services, builder.Configuration);

#if DEBUG && false
        AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
        AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.Always;
        AWSConfigs.LoggingConfig.LogMetrics = true;
        AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.JSON;
#endif

        var host = builder.Build();
        host.Run();
    }
}