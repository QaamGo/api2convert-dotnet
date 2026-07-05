using System;

namespace Api2Convert.Exceptions;

/// <summary>
/// Base class for every exception thrown by the SDK.
///
/// <para>Catch this to handle any SDK failure in one place; catch a more specific subclass
/// (e.g. <see cref="RateLimitException"/>, <see cref="ConversionFailedException"/>) to react to a
/// particular failure mode.</para>
/// </summary>
public class Api2ConvertException : Exception
{
    public Api2ConvertException(string message)
        : base(message)
    {
    }

    public Api2ConvertException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
