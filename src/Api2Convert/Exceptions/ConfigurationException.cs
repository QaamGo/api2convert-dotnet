namespace Api2Convert.Exceptions;

/// <summary>
/// The client was misconfigured — thrown before any request is made. The current case is
/// constructing an <see cref="Api2ConvertClient"/> without an API key (none passed and the
/// <c>API2CONVERT_API_KEY</c> environment variable unset).
///
/// <para>Derives from <see cref="Api2ConvertException"/> so a single <c>catch</c> handles every SDK
/// failure, configuration errors included.</para>
/// </summary>
public sealed class ConfigurationException : Api2ConvertException
{
    public ConfigurationException(string message)
        : base(message)
    {
    }
}
