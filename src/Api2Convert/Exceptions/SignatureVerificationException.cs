namespace Api2Convert.Exceptions;

/// <summary>
/// A webhook payload could not be verified or parsed: a missing or wrong signature, or a body that
/// is not a valid JSON object.
/// </summary>
public sealed class SignatureVerificationException : Api2ConvertException
{
    public SignatureVerificationException(string message)
        : base(message)
    {
    }
}
