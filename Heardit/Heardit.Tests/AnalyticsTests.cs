using Heardit.Services;

namespace Heardit.Tests;

public class AnalyticsTests
{
    [Theory]
    [InlineData("G-7F3HSB2L01", "G-7F3HSB2L01")]
    [InlineData("  G-ABCDEF1234  ", "G-ABCDEF1234")]      // trimmed
    [InlineData("G-ABCDEF", "G-ABCDEF")]                  // 6 characters is the minimum
    [InlineData("G-ABCDE", null)]                         // too short
    [InlineData("G-ABCDEFGHIJKLM", null)]                 // too long
    [InlineData("g-abcdef1234", null)]                    // lower case
    [InlineData("UA-123456-1", null)]                     // Universal Analytics, long gone
    [InlineData("G-ABCDEF1234\"></script><script>", null)] // can't break out of the attribute
    [InlineData("G-ABCDEF1234\n", "G-ABCDEF1234")]        // trailing whitespace is trimmed
    [InlineData("", null)]
    [InlineData(null, null)]
    public void MeasurementId_AcceptsOnlyGa4Ids(string? configured, string? expected)
    {
        Assert.Equal(expected, Analytics.MeasurementId(configured));
    }
}
