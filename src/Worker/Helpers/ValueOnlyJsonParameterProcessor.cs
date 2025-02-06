using Amazon.Extensions.Configuration.SystemsManager;
using Amazon.SimpleSystemsManagement.Model;

namespace Bounan.Downloader.Worker.Helpers;

/// <summary>
/// A fixed version of the <see cref="JsonParameterProcessor"/> that prevents using the parameter name as
/// the configuration prefix.
/// </summary>
public class ValueOnlyJsonParameterProcessor : JsonParameterProcessor
{
    /// <summary>
    /// Returns an empty string to prevent using the parameter name as the configuration prefix.
    /// Therefore, the configuration key will be set by JSON paths only (like appsettings.json).
    /// </summary>
    /// <param name="parameter">Parameter.</param>
    /// <param name="path">Source SSM parameter path.</param>
    /// <returns>Prefix for configuration keys.</returns>
    public override string GetKey(Parameter parameter, string path)
    {
        return string.Empty;
    }
}
