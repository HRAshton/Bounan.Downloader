using Bounan.Downloader.Worker.Interfaces;
using Bounan.Downloader.Worker.Services;

namespace Bounan.Downloader.Worker.Factories;

public partial class FfmpegFactory : IFfmpegFactory
{
    public FfmpegFactory(
        ILogger<FfmpegFactory> logger,
        IServiceProvider serviceProvider)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
    }

    private ILogger<FfmpegFactory> Logger { get; }

    private IServiceProvider ServiceProvider { get; }

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