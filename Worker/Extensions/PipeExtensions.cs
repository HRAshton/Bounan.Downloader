using System.IO.Pipelines;

namespace Bounan.Downloader.Worker.Extensions;

public static class PipeExtensions
{
	public static async Task PumpToAsync(
		this PipeReader pipeReader,
		Stream outputStream,
		CancellationToken cancellationToken)
	{
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