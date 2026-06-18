using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.App;

internal sealed record GameTelemetryOption(
    string GameId,
    string DisplayName);

internal static class GameTelemetryCatalog
{
    public const string F125GameId = "f1-25";

    private static readonly IReadOnlyList<GameTelemetryOption> OptionsValue =
    [
        new(F125GameId, "F1 25")
    ];

    public static string DefaultGameId => F125GameId;

    public static IReadOnlyList<GameTelemetryOption> Options => OptionsValue;

    public static string NormalizeGameId(string? gameId)
    {
        foreach (var option in OptionsValue)
        {
            if (string.Equals(option.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                return option.GameId;
            }
        }

        return DefaultGameId;
    }

    public static string GetDisplayName(string? gameId)
    {
        var normalized = NormalizeGameId(gameId);
        foreach (var option in OptionsValue)
        {
            if (string.Equals(option.GameId, normalized, StringComparison.Ordinal))
            {
                return option.DisplayName;
            }
        }

        return "F1 25";
    }

    public static IGameTelemetryAdapter CreateAdapter(string? gameId)
    {
        return NormalizeGameId(gameId) switch
        {
            F125GameId => new F125GameTelemetryAdapter(),
            _ => new F125GameTelemetryAdapter()
        };
    }
}
