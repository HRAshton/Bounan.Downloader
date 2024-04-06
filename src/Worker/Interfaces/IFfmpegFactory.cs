namespace Bounan.Downloader.Worker.Interfaces;

public interface IFfmpegFactory
{
    IFfmpegService CreateFfmpegService(CancellationToken cancellationToken);
}