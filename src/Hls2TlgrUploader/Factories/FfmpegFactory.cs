using Bounan.Downloader.Hls2TlgrUploader.Interfaces;
using Bounan.Downloader.Hls2TlgrUploader.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bounan.Downloader.Hls2TlgrUploader.Factories;

internal sealed partial class FfmpegFactory(
    ILogger<FfmpegFactory> logger,
    IServiceProvider serviceProvider)
    : IFfmpegFactory
{
    private ILogger<FfmpegFactory> Logger => logger;

    private IServiceProvider ServiceProvider => serviceProvider;

    public IFfmpegService CreateFfmpegService(CancellationToken cancellationToken)
    {
        var ffmpegService = (FfmpegService)ServiceProvider.GetRequiredService<IFfmpegService>();
        ffmpegService.Run(GetNextFileId(), cancellationToken);
        Log.FfmpegServiceCreated(Logger);
        return ffmpegService;
    }

    private static string GetNextFileId()
    {
        return Guid.NewGuid().ToString();
    }
}