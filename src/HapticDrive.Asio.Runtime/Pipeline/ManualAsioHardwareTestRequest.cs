namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record ManualAsioHardwareTestRequest(
    float FrequencyHz,
    TimeSpan Duration,
    float Amplitude = 0.5f,
    string Source = "manual test",
    string DurationMode = "manual",
    long? AcceptedPaddleEventSequence = null,
    string? PaddleSide = null,
    int? PaddleButtonId = null,
    long? AcceptedGearPulseId = null)
{
    public const float MinimumFrequencyHz = 10f;

    public const float MaximumFrequencyHz = 80f;

    public const int MinimumDurationMilliseconds = 10;

    public static TimeSpan MaximumDuration { get; } = TimeSpan.FromSeconds(1);

    public string SignalName => $"{FrequencyHz:0.#} Hz sine";

    public float StrengthPercent => Amplitude * 100f;

    public int DurationMilliseconds => (int)Math.Round(Duration.TotalMilliseconds);

    public bool IsSupportedFrequency => FrequencyHz is >= MinimumFrequencyHz and <= MaximumFrequencyHz;

    public ManualAsioHardwareTestRequest Normalize()
    {
        var frequency = float.IsFinite(FrequencyHz)
            ? Math.Clamp(FrequencyHz, MinimumFrequencyHz, MaximumFrequencyHz)
            : 50f;
        var duration = Duration <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(45)
            : Duration > MaximumDuration
                ? MaximumDuration
                : Duration < TimeSpan.FromMilliseconds(MinimumDurationMilliseconds)
                    ? TimeSpan.FromMilliseconds(MinimumDurationMilliseconds)
                : Duration;
        var amplitude = float.IsFinite(Amplitude)
            ? Math.Clamp(Amplitude, 0f, 1f)
            : 0.5f;
        var source = string.IsNullOrWhiteSpace(Source)
            ? "manual test"
            : Source.Trim();
        var durationMode = string.IsNullOrWhiteSpace(DurationMode)
            ? "manual"
            : DurationMode.Trim();

        return this with
        {
            FrequencyHz = frequency,
            Duration = duration,
            Amplitude = amplitude,
            Source = source,
            DurationMode = durationMode
        };
    }
}
