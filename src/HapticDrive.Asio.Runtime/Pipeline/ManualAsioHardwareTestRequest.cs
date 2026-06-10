namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record ManualAsioHardwareTestRequest(
    float FrequencyHz,
    TimeSpan Duration,
    float Amplitude = 0.5f)
{
    public static TimeSpan MaximumDuration { get; } = TimeSpan.FromSeconds(1);

    public string SignalName => $"{FrequencyHz:0.#} Hz sine";

    public bool IsSupportedFrequency => FrequencyHz is 40f or 50f;

    public ManualAsioHardwareTestRequest Normalize()
    {
        var frequency = FrequencyHz switch
        {
            40f => 40f,
            50f => 50f,
            _ => FrequencyHz
        };
        var duration = Duration <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(250)
            : Duration > MaximumDuration
                ? MaximumDuration
                : Duration;
        var amplitude = float.IsFinite(Amplitude)
            ? Math.Clamp(Amplitude, 0f, 1f)
            : 0.5f;

        return this with
        {
            FrequencyHz = frequency,
            Duration = duration,
            Amplitude = amplitude
        };
    }
}
