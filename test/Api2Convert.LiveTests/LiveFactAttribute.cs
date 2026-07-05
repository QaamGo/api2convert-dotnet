using System;
using Xunit;

namespace Api2Convert.LiveTests;

/// <summary>
/// A <see cref="FactAttribute"/> that auto-skips unless <c>API2CONVERT_API_KEY</c> is set, so the
/// live suite is safe to run without a key. Export the behat default key to run it:
/// <c>API2CONVERT_API_KEY=&lt;key&gt; dotnet test test/Api2Convert.LiveTests</c>.
/// </summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("API2CONVERT_API_KEY")))
        {
            Skip = "live tests require API2CONVERT_API_KEY (export the behat default key to run)";
        }
    }
}
