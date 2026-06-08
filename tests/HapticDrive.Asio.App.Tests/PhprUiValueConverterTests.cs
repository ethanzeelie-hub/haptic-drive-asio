using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprUiValueConverterTests
{
    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(10d, 0.10d)]
    [InlineData(100d, 1.0d)]
    public void PercentToRatio_MapsUserPercentToInternalRatio(double percent, double expectedRatio)
    {
        Assert.Equal(expectedRatio, PhprUiValueConverter.PercentToRatio(percent), precision: 6);
    }

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(0.10d, 10d)]
    [InlineData(1.0d, 100d)]
    public void RatioToPercent_MapsInternalRatioToUserPercent(double ratio, double expectedPercent)
    {
        Assert.Equal(expectedPercent, PhprUiValueConverter.RatioToPercent(ratio), precision: 6);
    }

    [Fact]
    public void TryParseFrequencyHz_AllowsOnlyOneToFiftyHz()
    {
        Assert.True(PhprUiValueConverter.TryParseFrequencyHz("50", "Brake", out var frequency, out _));
        Assert.Equal(50d, frequency);

        Assert.False(PhprUiValueConverter.TryParseFrequencyHz("51", "Brake", out _, out var highMessage));
        Assert.Contains("1 to 50", highMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("10", true)]
    [InlineData("1000", true)]
    [InlineData("9", false)]
    [InlineData("1001", false)]
    public void TryParseDurationMs_UsesUserPulseRange(string text, bool expected)
    {
        Assert.Equal(expected, PhprUiValueConverter.TryParseDurationMs(text, "Brake", out _, out _));
    }
}
