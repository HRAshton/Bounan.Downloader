using Amazon.Extensions.Configuration.SystemsManager;
using Bounan.Downloader.Worker.Helpers;
using Bounan.LoanApi;
using Hls2TlgrUploader;

namespace Bounan.Downloader.Worker;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.Sources.Add(
            new SystemsManagerConfigurationSource
            {
                Path = "/bounan/downloader/runtime-config/",
                AwsOptions = builder.Configuration.GetAWSOptions(),
                ParameterProcessor = new ValueOnlyJsonParameterProcessor(),
            });
        builder.Configuration.AddEnvironmentVariables();

        builder.Services
            .AddLoanApiDownloadingServices(cfg => builder.Configuration.GetSection("LoanApi").Bind(cfg))
            .AddHls2TlgrUploader(builder.Configuration.GetRequiredSection("Hls2TlgrUploader"))
            .AddWorkerServices(builder.Configuration);

#if DEBUG && false
        AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
        AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.Always;
        AWSConfigs.LoggingConfig.LogMetrics = true;
        AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.JSON;
#endif

        builder.Build().Run();
    }
}
