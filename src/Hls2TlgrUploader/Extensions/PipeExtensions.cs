using System.IO.Pipelines;

namespace Bounan.Downloader.Hls2TlgrUploader.Extensions;

public static class PipeExtensions
{
    public static async Task PumpToAsync(
        this PipeReader pipeReader,
        Stream outputStream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeReader, nameof(pipeReader));
        ArgumentNullException.ThrowIfNull(outputStream, nameof(outputStream));

        while (true)
        {
            var result = await pipeReader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            foreach (var segment in buffer)
            {
                await outputStream.WriteAsync(segment, cancellationToken);
            }

            pipeReader.AdvanceTo(buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        outputStream.Close();
    }
}