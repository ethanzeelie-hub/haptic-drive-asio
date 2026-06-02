namespace HapticDrive.Asio.Audio.TestBench;

public sealed class AudioTestSignalDefinition
{
    public AudioTestSignalDefinition(
        AudioTestSignalKind kind,
        float amplitude = 0.5f,
        float frequencyHz = 50f,
        float sweepStartFrequencyHz = 20f,
        float sweepEndFrequencyHz = 120f,
        double sweepDurationSeconds = 2.0,
        int pulseIntervalFrames = 4_800,
        int pulseWidthFrames = 1,
        float constantValue = 0.25f)
    {
        if (!float.IsFinite(amplitude))
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude), "Amplitude must be finite.");
        }

        if (!float.IsFinite(frequencyHz) || frequencyHz <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), "Frequency must be finite and positive.");
        }

        if (!float.IsFinite(sweepStartFrequencyHz) || sweepStartFrequencyHz <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(sweepStartFrequencyHz), "Sweep start frequency must be finite and positive.");
        }

        if (!float.IsFinite(sweepEndFrequencyHz) || sweepEndFrequencyHz <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(sweepEndFrequencyHz), "Sweep end frequency must be finite and positive.");
        }

        if (!double.IsFinite(sweepDurationSeconds) || sweepDurationSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(sweepDurationSeconds), "Sweep duration must be finite and positive.");
        }

        if (pulseIntervalFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pulseIntervalFrames), "Pulse interval must be positive.");
        }

        if (pulseWidthFrames <= 0 || pulseWidthFrames > pulseIntervalFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(pulseWidthFrames), "Pulse width must be positive and no larger than the pulse interval.");
        }

        if (!float.IsFinite(constantValue))
        {
            throw new ArgumentOutOfRangeException(nameof(constantValue), "Constant value must be finite.");
        }

        Kind = kind;
        Amplitude = amplitude;
        FrequencyHz = frequencyHz;
        SweepStartFrequencyHz = sweepStartFrequencyHz;
        SweepEndFrequencyHz = sweepEndFrequencyHz;
        SweepDurationSeconds = sweepDurationSeconds;
        PulseIntervalFrames = pulseIntervalFrames;
        PulseWidthFrames = pulseWidthFrames;
        ConstantValue = constantValue;
    }

    public AudioTestSignalKind Kind { get; }

    public float Amplitude { get; }

    public float FrequencyHz { get; }

    public float SweepStartFrequencyHz { get; }

    public float SweepEndFrequencyHz { get; }

    public double SweepDurationSeconds { get; }

    public int PulseIntervalFrames { get; }

    public int PulseWidthFrames { get; }

    public float ConstantValue { get; }

    public string DisplayName => Kind switch
    {
        AudioTestSignalKind.Silence => "Silence",
        AudioTestSignalKind.SineTone => $"{FrequencyHz:0.#} Hz sine tone",
        AudioTestSignalKind.FrequencySweep => $"{SweepStartFrequencyHz:0.#}-{SweepEndFrequencyHz:0.#} Hz sweep",
        AudioTestSignalKind.Pulse => "Pulse transient",
        AudioTestSignalKind.Constant => "Constant value",
        _ => Kind.ToString()
    };

    public static AudioTestSignalDefinition DefaultFor(AudioTestSignalKind kind)
    {
        return kind switch
        {
            AudioTestSignalKind.Silence => new AudioTestSignalDefinition(kind, amplitude: 0f),
            AudioTestSignalKind.SineTone => new AudioTestSignalDefinition(kind, amplitude: 0.5f, frequencyHz: 50f),
            AudioTestSignalKind.FrequencySweep => new AudioTestSignalDefinition(
                kind,
                amplitude: 0.4f,
                sweepStartFrequencyHz: 20f,
                sweepEndFrequencyHz: 120f,
                sweepDurationSeconds: 2.0),
            AudioTestSignalKind.Pulse => new AudioTestSignalDefinition(
                kind,
                amplitude: 0.75f,
                pulseIntervalFrames: 4_800,
                pulseWidthFrames: 1),
            AudioTestSignalKind.Constant => new AudioTestSignalDefinition(
                kind,
                constantValue: 0.25f),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown test signal kind.")
        };
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
